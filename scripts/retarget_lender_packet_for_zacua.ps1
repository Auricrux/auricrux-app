$ErrorActionPreference = 'Stop'

$src = 'C:\Users\Auricrux\OneDrive - Future Contractors of America LLC\Copilot\Created\FCA_Lender_Packet_v2_ProductProof.pptx'
$dst = 'C:\repos\auricrux-app\FCA_Zacua_Pitch_Deck_Aligned_v5.pptx'
$pdf = 'C:\repos\auricrux-app\FCA_Zacua_Pitch_Deck_Aligned_v5.pdf'

$map = @{
  'Lender + Backer Packet' = 'Investor Pitch Deck'
  'Lender Packet' = 'Investor Deck'
  'Lender' = 'Investor'
  'Backer' = 'Partner'
}

function Replace-ShapeText {
  param($shape)

  try {
    if ($shape.HasTextFrame -and $shape.TextFrame.HasText) {
      $t = [string]$shape.TextFrame.TextRange.Text
      foreach ($k in $map.Keys) { $t = $t.Replace($k, $map[$k]) }
      $shape.TextFrame.TextRange.Text = $t
    }
  } catch {}

  try {
    if ($shape.Type -eq 6) {
      foreach ($gs in @($shape.GroupItems)) { Replace-ShapeText -shape $gs }
    }
  } catch {}

  try {
    if ($shape.HasTable) {
      $rows = $shape.Table.Rows.Count
      $cols = $shape.Table.Columns.Count
      for ($r = 1; $r -le $rows; $r++) {
        for ($c = 1; $c -le $cols; $c++) {
          $cell = $shape.Table.Cell($r, $c)
          $t = [string]$cell.Shape.TextFrame.TextRange.Text
          foreach ($k in $map.Keys) { $t = $t.Replace($k, $map[$k]) }
          $cell.Shape.TextFrame.TextRange.Text = $t
        }
      }
    }
  } catch {}
}

$pp = New-Object -ComObject PowerPoint.Application
$pp.Visible = -1
$pres = $pp.Presentations.Open($src, $false, $false, $false)

foreach ($slide in @($pres.Slides)) {
  foreach ($shape in @($slide.Shapes)) {
    Replace-ShapeText -shape $shape
  }
}

$pres.SaveAs($dst, 24)
$pres.SaveAs($pdf, 32)
$pres.Close()
$pp.Quit()

$target = 'C:\Users\Auricrux\OneDrive - Future Contractors of America LLC\Applications\Zacua Ventures'
New-Item -ItemType Directory -Path $target -Force | Out-Null
Copy-Item $dst (Join-Path $target 'FCA_Zacua_Pitch_Deck_Aligned_v5.pptx') -Force
Copy-Item $pdf (Join-Path $target 'FCA_Zacua_Pitch_Deck_Aligned_v5.pdf') -Force

Get-Item $dst, $pdf, (Join-Path $target 'FCA_Zacua_Pitch_Deck_Aligned_v5.pptx'), (Join-Path $target 'FCA_Zacua_Pitch_Deck_Aligned_v5.pdf') |
  Select-Object FullName, Length, LastWriteTime
