#requires -Version 5.1
<#
Turn a resolved Windows icon path into an RC string-safe path.
Windows resource files treat backslashes in quoted strings as escapes, so
normalise every individual separator instead of searching for '\\'.
#>
function ConvertTo-NativeIconResourcePath {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw 'Native icon path is empty.'
    }
    $unsafe = [char[]]@([char]34, [char]10, [char]13)
    if ($Path.IndexOfAny($unsafe) -ge 0) {
        throw 'Native icon path cannot contain quotes or line breaks.'
    }

    return $Path.Replace([char]92, [char]47)
}

# rc.exe defaults to the machine ANSI code page. Keep source paths intact by
# declaring UTF-8 inside the resource script, and writing UTF-8 without a BOM.
function Write-NativeIconResourceFile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$OutputPath,
        [Parameter(Mandatory = $true)][string]$IconPath
    )

    $resourcePath = ConvertTo-NativeIconResourcePath -Path $IconPath
    $resourceText = "#pragma code_page(65001)`r`n1 ICON `"$resourcePath`"`r`n"
    [IO.File]::WriteAllText(
        $OutputPath,
        $resourceText,
        [Text.UTF8Encoding]::new($false)
    )
}
