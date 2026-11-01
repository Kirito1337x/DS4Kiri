using System.Diagnostics;
using System.Text;

namespace DualSenseApp.Services
{
    public static class DriverInstallerService
    {
        private const string InstallScript = """
            $ErrorActionPreference = 'Stop'
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

            if (-not [Environment]::Is64BitOperatingSystem) {
                throw 'I driver Nefarius supportati da questa app richiedono Windows a 64 bit.'
            }

            $packages = @()
            if (__INSTALL_VIGEM__) {
                Write-Warning 'ViGEmBus e'' un progetto archiviato: viene usata solo la release ufficiale disponibile.'
                $packages += [pscustomobject]@{
                    Repository = 'nefarius/ViGEmBus'
                    AssetPattern = '^ViGEmBus_[0-9.]+_x64_x86_arm64\.exe$'
                    Arguments = @()
                    Name = 'ViGEmBus'
                }
            }
            if (__INSTALL_HIDHIDE__) {
                $packages += [pscustomobject]@{
                    Repository = 'nefarius/HidHide'
                    AssetPattern = '^HidHide_[0-9.]+_x64\.exe$'
                    Arguments = @('/exenoui', '/qn', '/norestart')
                    Name = 'HidHide'
                }
            }

            $downloadDirectory = Join-Path $env:TEMP ('DualSenseApp-Drivers-' + [Guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $downloadDirectory | Out-Null
            $rebootRequired = $false

            try {
                foreach ($package in $packages) {
                    Write-Host ('Ricerca release ufficiale: ' + $package.Name)
                    $releaseUri = 'https://api.github.com/repos/' + $package.Repository + '/releases/latest'
                    $release = Invoke-RestMethod -Uri $releaseUri -Headers @{
                        'User-Agent' = 'DualSenseApp-DriverInstaller'
                        'Accept' = 'application/vnd.github+json'
                    }

                    if ($release.draft -or $release.prerelease) {
                        throw ('La release di ' + $package.Name + ' non e'' una release stabile.')
                    }

                    $asset = $release.assets |
                        Where-Object { $_.name -match $package.AssetPattern } |
                        Select-Object -First 1
                    if (-not $asset) {
                        throw ('Installer ufficiale x64 non trovato nella release di ' + $package.Name + '.')
                    }

                    $downloadUri = [Uri]$asset.browser_download_url
                    $expectedPrefix = '/' + $package.Repository + '/releases/download/' + $release.tag_name + '/'
                    if ($downloadUri.Scheme -ne 'https' -or
                        $downloadUri.Host -ne 'github.com' -or
                        -not $downloadUri.AbsolutePath.StartsWith($expectedPrefix, [StringComparison]::Ordinal)) {
                        throw ('URL di download non valido per ' + $package.Name + '.')
                    }

                    $installerPath = Join-Path $downloadDirectory $asset.name
                    Write-Host ('Download ' + $package.Name + ' ' + $release.tag_name + ' da GitHub ufficiale...')
                    Invoke-WebRequest -Uri $downloadUri.AbsoluteUri -OutFile $installerPath -UseBasicParsing

                    $signature = Get-AuthenticodeSignature -FilePath $installerPath
                    if ($signature.Status -ne 'Valid' -or
                        -not $signature.SignerCertificate.Subject.Contains('Nefarius Software Solutions')) {
                        throw ('Firma digitale Nefarius non valida per ' + $package.Name + '. Installazione annullata.')
                    }

                    Write-Host ('Avvio setup firmato: ' + $package.Name)
                    if ($package.Arguments.Count -gt 0) {
                        $installer = Start-Process -FilePath $installerPath `
                            -ArgumentList $package.Arguments -Wait -PassThru
                    }
                    else {
                        $installer = Start-Process -FilePath $installerPath -Wait -PassThru
                    }

                    if ($installer.ExitCode -notin @(0, 1641, 3010)) {
                        throw ('Il setup di ' + $package.Name + ' e'' terminato con codice ' + $installer.ExitCode + '.')
                    }

                    if ($installer.ExitCode -in @(1641, 3010)) {
                        $rebootRequired = $true
                        Write-Host ('Riavvio di Windows richiesto da ' + $package.Name + '.')
                        break
                    }
                }
            }
            finally {
                Remove-Item -LiteralPath $downloadDirectory -Recurse -Force -ErrorAction SilentlyContinue
            }

            if ($rebootRequired) { exit 3010 }
            Write-Host 'Installazione driver completata.'
            exit 0
            """;

        public static bool Install(bool vigem, bool hidHide)
        {
            if (!vigem && !hidHide)
                throw new ArgumentException("Selezionare almeno un driver da installare.");

            string script = InstallScript
                .Replace("__INSTALL_VIGEM__", vigem ? "$true" : "$false", StringComparison.Ordinal)
                .Replace("__INSTALL_HIDHIDE__", hidHide ? "$true" : "$false", StringComparison.Ordinal);
            string encodedScript = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encodedScript}",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Normal
            };

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Impossibile avviare PowerShell per installare i driver.");
            process.WaitForExit();

            return process.ExitCode switch
            {
                0 => false,
                3010 or 1641 => true,
                _ => throw new InvalidOperationException(
                    $"PowerShell ha terminato l'installazione dei driver con codice {process.ExitCode}.")
            };
        }
    }
}
