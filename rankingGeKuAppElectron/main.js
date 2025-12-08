const { app, BrowserWindow } = require('electron');
const path = require('path');
const { spawn } = require('child_process');

let mainWindow;
let backendProcess;

// Dev = alles ausser production
//const isDev = !app.isPackaged;

//const isDev = process.env.NODE_ENV !== 'production';

const isDev = false; // Temporär für Tests, bis Packaging klappt

// Pfade relativ zu diesem File
const appRoot = app.isPackaged ? app.getAppPath() : __dirname;
const appPath = path.join(appRoot, 'app');
const backendPath = app.isPackaged
  ? path.join(process.resourcesPath, 'backend') // liegt ausserhalb der asar
  : path.join(appPath, 'backend');
const frontendPath = path.join(appPath, 'dist', 'browser');
const assetsPath = app.isPackaged
  ? path.join(process.resourcesPath, 'assets')
  : path.join(appRoot, 'assets');

function startBackend() {
  // HIER den exakten Namen deiner gepublishten Binary eintragen
  // ls app/backend → z.B. Backend_RankingGeKu
  let backendExe;

  if (process.platform === 'win32') {
    // Windows: exe
    backendExe = 'Backend_RankingGeKu.exe';
  } else {
    // macOS / Linux: ohne .exe
    backendExe = 'Backend_RankingGeKu';
  }

  const exePath = path.join(backendPath, backendExe);
  console.log('Starte Backend:', exePath);

  // In macOS-Apps ist PATH oft minimal; Homebrew liegt dann nicht drin.
  const env = { ...process.env };
  if (process.platform === 'darwin') {
    const brewPath = '/opt/homebrew/bin';
    env.PATH = [brewPath, env.PATH].filter(Boolean).join(':');
  }

  backendProcess = spawn(exePath, ['--urls', 'http://127.0.0.1:5157'], {
    cwd: backendPath,
    env
  });

  backendProcess.stdout.on('data', (data) => {
    console.log(`[BACKEND]: ${data}`);
  });

  backendProcess.stderr.on('data', (data) => {
    console.error(`[BACKEND ERROR]: ${data}`);
  });

  backendProcess.on('close', (code) => {
    console.log(`[BACKEND] exited with code ${code}`);
  });
}

function createWindow() {
  console.log('createWindow aufgerufen, isDev =', isDev);

  // macOS Dock-Icon explizit setzen (BrowserWindow.icon greift dort nicht)
  if (process.platform === 'darwin') {
    const dockIcon = path.join(assetsPath, 'icons', 'icon.icns');
    try {
      if (app.dock && dockIcon) app.dock.setIcon(dockIcon);
    } catch (e) {
      console.error('Dock-Icon konnte nicht gesetzt werden:', e);
    }
  }

  mainWindow = new BrowserWindow({
  width: 1200,
  height: 800,
  icon: path.join(
    assetsPath,
    'icons',
    process.platform === 'win32' ? 'icon.png' : 'icon.icns'
  ),
  webPreferences: {
    nodeIntegration: false,
    contextIsolation: true
    }
  });

  if (isDev) {
    // DEV: Angular Dev-Server
    mainWindow.loadURL('http://localhost:4200');
    mainWindow.webContents.openDevTools();
  } else {
    // PROD: gebaute Angular-App
    const indexPath = path.join(frontendPath, 'index.html');
    console.log('Lade index.html aus', indexPath);
    mainWindow.loadFile(indexPath);
    // DevTools in Prod normalerweise aus, bei Bedarf für Debug:
    // mainWindow.webContents.openDevTools();
  }

  // Hilfreiche Logs bei Ladevorgängen
  mainWindow.webContents.on('did-fail-load', (_e, code, desc, url) => {
    console.error('did-fail-load', { code, desc, url });
  });
  mainWindow.webContents.on('did-finish-load', () => {
    console.log('did-finish-load');
  });
  mainWindow.webContents.on('console-message', (_e, level, message, line, sourceId) => {
    console.log(`renderer console [${level}] ${sourceId}:${line} → ${message}`);
  });

  mainWindow.on('closed', () => {
    mainWindow = null;
  });
}



app.whenReady().then(() => {
  console.log('Electron app ready');

  if (!isDev) {
    // In Production das Backend starten
    startBackend();
  } else {
    console.log('DEV-Modus: Backend wird extern (dotnet run) erwartet.');
  }

  createWindow();

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      createWindow();
    }
  });
});

app.on('window-all-closed', () => {
  // macOS: App läuft normalerweise weiter, bis der User explizit beendet
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

app.on('before-quit', () => {
  console.log('before-quit');
  if (backendProcess && !backendProcess.killed) {
    console.log('Backend-Prozess beenden');
    backendProcess.kill();
  }
});
