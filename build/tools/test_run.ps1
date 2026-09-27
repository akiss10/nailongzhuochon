Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$exe = Join-Path (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)) "dist\黄色小宠物.exe"
$pre = Join-Path (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)) "preview"

function Shot([string]$path) {
    $vs = [System.Windows.Forms.SystemInformation]::VirtualScreen
    $bmp = New-Object System.Drawing.Bitmap($vs.Width, $vs.Height)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($vs.Left, $vs.Top, 0, 0, $bmp.Size)
    # mark the real cursor position with a small cross
    $cp = [System.Windows.Forms.Cursor]::Position
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::Red, 2)
    $g.DrawLine($pen, $cp.X - $vs.Left - 14, $cp.Y - $vs.Top, $cp.X - $vs.Left + 14, $cp.Y - $vs.Top)
    $g.DrawLine($pen, $cp.X - $vs.Left, $cp.Y - $vs.Top - 14, $cp.X - $vs.Left, $cp.Y - $vs.Top + 14)
    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "saved $path (cursor $($cp.X),$($cp.Y))"
}

$before = [System.Windows.Forms.Cursor]::Position
Write-Host "screen: $([System.Windows.Forms.SystemInformation]::VirtualScreen)"

$proc = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 3
Shot (Join-Path $pre "test_1_start.png")

[System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point(700, 300)
Start-Sleep -Milliseconds 600
Shot (Join-Path $pre "test_2_cursor.png")

[System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point(1100, 700)
Start-Sleep -Milliseconds 600
Shot (Join-Path $pre "test_3_cursor.png")

Write-Host "running: $(-not $proc.HasExited)"
[System.Windows.Forms.Cursor]::Position = $before
