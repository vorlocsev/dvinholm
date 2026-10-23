#requires -Version 5.1
<#
Read-only PRE-SIGN inspection of an exact raw public DER .cer only.
Checks MSIX manifest Publisher matches a public X.509 code-signing
certificate, without loading a private key, signing a file, trusting a
certificate, or installing any package. Does NOT verify trust or signature.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackagePath,
    [Parameter(Mandatory)][string]$CertificatePath
)
$ErrorActionPreference = 'Stop'
$package = (Resolve-Path -LiteralPath $PackagePath -ErrorAction Stop).Path
$certificateFile = (Resolve-Path -LiteralPath $CertificatePath -ErrorAction Stop).Path
if ([IO.Path]::GetExtension($certificateFile) -cne '.cer') {
    throw 'Use only a public DER .cer certificate; private-key formats are prohibited.'
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($package)
try {
    $entry = $archive.GetEntry('AppxManifest.xml')
    if (-not $entry) { throw 'MSIX manifest is missing.' }
    $reader = New-Object IO.StreamReader($entry.Open())
    try { [xml]$manifest = $reader.ReadToEnd() }
    finally { $reader.Dispose() }
} finally { $archive.Dispose() }
$ns = New-Object Xml.XmlNamespaceManager($manifest.NameTable)
$ns.AddNamespace('a', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10')
$identities = @($manifest.SelectNodes('/a:Package/a:Identity', $ns))
if ($identities.Count -ne 1 -or
    $identities[0].GetAttribute('Name') -ne 'Dvinholm.Lite' -or
    $identities[0].GetAttribute('ProcessorArchitecture') -ne 'x86') {
    throw 'MSIX identity is not exactly Dvinholm.Lite x86.'
}
$publisher = $identities[0].GetAttribute('Publisher')
if ([string]::IsNullOrWhiteSpace($publisher)) {
    throw 'MSIX publisher is missing.'
}
try {
    $cert = [Security.Cryptography.X509Certificates.X509Certificate2]::new($certificateFile)
} catch {
    throw 'Cannot read a valid public DER .cer certificate.'
}
try {
    if ($cert.HasPrivateKey) {
        throw 'Public certificate unexpectedly contains a private key.'
    }
    # Extension alone is not sufficient: X509Certificate2 may load a wrapper,
    # a container or appended data while returning only the public cert as
    # RawData. Require an exact DER certificate and no trailing bytes.
    [byte[]]$fileBytes = [IO.File]::ReadAllBytes($certificateFile)
    [byte[]]$derBytes = $cert.RawData
    if ($fileBytes.Length -ne $derBytes.Length -or
        [Convert]::ToBase64String($fileBytes) -cne [Convert]::ToBase64String($derBytes)) {
        throw 'Public certificate must contain exact raw DER .cer bytes without wrapper or trailing data.'
    }
    if (-not [string]::Equals($cert.Subject, $publisher, [StringComparison]::Ordinal)) {
        throw "Certificate Subject does not exactly match MSIX Publisher."
    }
    $now = [DateTime]::UtcNow
    if ($cert.NotBefore.ToUniversalTime() -gt $now) {
        throw 'Certificate is not yet valid.'
    }
    if ($cert.NotAfter.ToUniversalTime() -le $now) {
        throw 'Certificate is expired.'
    }
    $eku = @($cert.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.37' })
    if ($eku.Count -ne 1) { throw 'Certificate must declare code-signing EKU.' }
    $codeSigning = '1.3.6.1.5.5.7.3.3'
    $ekuValues = @($eku[0].EnhancedKeyUsages | ForEach-Object { $_.Value })
    if ($ekuValues.Count -ne 1 -or $ekuValues[0] -ne $codeSigning) {
        throw 'Certificate must declare code-signing EKU only.'
    }
    $usage = @($cert.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.15' })
    if ($usage.Count -ne 1) { throw 'Certificate must declare digitalSignature key usage.' }
    $digitalSignature = [int][Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature
    if (([int]$usage[0].KeyUsages -band $digitalSignature) -eq 0) {
        throw 'Certificate lacks digitalSignature key usage.'
    }
    $basic = @($cert.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.19' })
    if ($basic.Count -ne 1 -or $basic[0].CertificateAuthority) {
        throw 'Certificate must be an end-entity signing certificate, not a CA.'
    }
    Write-Host "Public code-signing certificate metadata matches MSIX Publisher '$publisher'."
    Write-Host 'PRE-SIGN CHECK ONLY: no private key, signing, signature verification, chain trust, installation or GUI launch was tested.'
} finally { $cert.Dispose() }
