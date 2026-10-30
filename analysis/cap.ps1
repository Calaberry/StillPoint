param(
    [string]$Out = "D:\dsh file\StillPoint\analysis\shots\shot.png"
)
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
$dir = Split-Path $Out -Parent
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($b.X, $b.Y, 0, 0, $bmp.Size)
$g.Dispose()
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Output "captured $($b.Width)x$($b.Height) -> $Out"
