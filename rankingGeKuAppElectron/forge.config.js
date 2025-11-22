const path = require('path');
const { FusesPlugin } = require('@electron-forge/plugin-fuses');
const { FuseV1Options, FuseVersion } = require('@electron/fuses');

module.exports = {
  packagerConfig: {
    asar: true,
    // Icon für App (macOS .icns, Windows .ico) – basename ohne Endung
    icon: path.join(__dirname, 'assets', 'icons', 'icon'),
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
      config: {},
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
};
