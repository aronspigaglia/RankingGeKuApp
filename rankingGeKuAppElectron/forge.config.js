const path = require('path');
const { FusesPlugin } = require('@electron-forge/plugin-fuses');
const { FuseV1Options, FuseVersion } = require('@electron/fuses');
const pkg = require('./package.json');

const entitlementsPath = path.join(__dirname, 'entitlements.plist');

module.exports = {
  packagerConfig: {
    asar: true,
    // Icon für App (macOS .icns, Windows .ico) – basename ohne Endung
    icon: path.join(__dirname, 'assets', 'icons', 'icon'),
    // macOS: Signieren + Notarisieren (Zertifikate + Notary-Creds per ENV)
    osxSign: {
      identity: process.env.APPLE_IDENTITY || 'Developer ID Application',
      hardenedRuntime: true,
      entitlements: entitlementsPath,
      'entitlements-inherit': entitlementsPath,
      gatekeeperAssess: false,
    },
    osxNotarize: {
      tool: 'notarytool',
      // Bevorzugt Keychain-Profile (z.B. "AC_PASSWORD"); fällt sonst auf ENV zurück
      keychainProfile: process.env.APPLE_KEYCHAIN_PROFILE || 'AC_PASSWORD',
      appleId: process.env.APPLE_ID,
      appleIdPassword: process.env.APPLE_APP_SPECIFIC_PASSWORD,
      teamId: process.env.APPLE_TEAM_ID,
    },
    /**
     * Zusätzliche Ressourcen, die neben der asar in die App gelegt werden.
     * Hier: das gepublishte .NET-Backend, das du nach app/backend ausgibst.
     */
    extraResource: [
      path.resolve(__dirname, 'app', 'backend'),
      path.resolve(__dirname, 'assets'),        // Icons auch ausserhalb der asar verfügbar machen
    ],
  },
  rebuildConfig: {},
  makers: [
    {
      name: '@electron-forge/maker-squirrel',   // Windows-Installer (später)
      config: {
        // Klarerer Dateiname für Windows-Installer
        setupExe: 'RankingGeKu-win-x64-Setup.exe',
        setupMsi: 'RankingGeKu-win-x64-Setup.msi',
      },
    },
    {
      name: '@electron-forge/maker-zip',        // ZIP für macOS
      platforms: ['darwin'],
    },
    {
      name: '@electron-forge/maker-deb',        // Linux (.deb)
      config: {},
    },
    {
      name: '@electron-forge/maker-rpm',        // Linux (.rpm)
      config: {},
    },
  ],
  plugins: [
    {
      name: '@electron-forge/plugin-auto-unpack-natives',
      config: {},
    },
    // Fuses: Sicherheits-Features von Electron vor dem Signieren konfigurieren
    new FusesPlugin({
      version: FuseVersion.V1,
      [FuseV1Options.RunAsNode]: false,
      [FuseV1Options.EnableCookieEncryption]: true,
      [FuseV1Options.EnableNodeOptionsEnvironmentVariable]: false,
      [FuseV1Options.EnableNodeCliInspectArguments]: false,
      [FuseV1Options.EnableEmbeddedAsarIntegrityValidation]: true,
      [FuseV1Options.OnlyLoadAppFromAsar]: true,
    }),
  ],
  hooks: {
    /**
     * Benennt das macOS arm64 ZIP um, damit klar erkennbar ist, dass es fürs M‑Chip-Build ist.
     */
    async postMake(_forgeConfig, makeResults) {
      const fs = require('fs/promises');
      for (const result of makeResults) {
        if (result.platform === 'darwin' && result.arch === 'arm64') {
          for (let i = 0; i < result.artifacts.length; i++) {
            const artifact = result.artifacts[i];
            if (artifact.endsWith('.zip')) {
              const dir = path.dirname(artifact);
              const target = path.join(dir, `RankingGeKu-mac-m-chip-${pkg.version}.zip`);
              await fs.rename(artifact, target);
              result.artifacts[i] = target;
              console.log(`Renamed mac artifact: ${path.basename(target)}`);
            }
          }
        }
      }
    }
  }
};
