param([Parameter(Mandatory = $true)][string]$Path)

$stream = [System.IO.File]::OpenRead((Resolve-Path $Path))
$reader = [System.IO.BinaryReader]::new($stream)
try {
    if ($reader.ReadUInt16() -ne 0x5A4D) {
        throw "$Path is not a Windows PE executable."
    }
    $stream.Position = 0x3C
    $peOffset = $reader.ReadInt32()
    if ($peOffset -lt 0x40 -or $peOffset -gt $stream.Length - 94) {
        throw "$Path has an invalid PE header offset."
    }
    $stream.Position = $peOffset
    if ($reader.ReadUInt32() -ne 0x00004550) {
        throw "$Path has no PE signature."
    }
    $stream.Position = $peOffset + 24
    $magic = $reader.ReadUInt16()
    if ($magic -ne 0x10B -and $magic -ne 0x20B) {
        throw "$Path has an unsupported PE optional header."
    }
    $stream.Position = $peOffset + 24 + 68
    $subsystem = $reader.ReadUInt16()
    if ($subsystem -ne 2) {
        throw "$Path uses PE subsystem $subsystem instead of Windows GUI (2)."
    }
    Write-Host "$Path uses the Windows GUI subsystem."
}
finally {
    $reader.Dispose()
    $stream.Dispose()
}
