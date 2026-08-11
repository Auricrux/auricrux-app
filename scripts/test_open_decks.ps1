$ErrorActionPreference = 'Continue'

$files = @(
  'C:\Users\Auricrux\OneDrive - Future Contractors of America LLC\Copilot\Created\About_FCA_FINAL.pptx',
  'C:\Users\Auricrux\OneDrive - Future Contractors of America LLC\Copilot\Created\FCA_At_A_Glance_CONSOLIDATED_FINAL.pptx',
  'C:\Users\Auricrux\OneDrive - Future Contractors of America LLC\Copilot\Created\FCA_Lender_Packet_v2_ProductProof.pptx',
  'C:\Users\Auricrux\OneDrive - Future Contractors of America LLC\Copilot\Created\FCA_Flashy_Lender_Packet_Deck.pptx',
  'C:\Users\Auricrux\OneDrive - Future Contractors of America LLC\Copilot\Created\Future_Contractors_of_America_LLC_Pitch_Deck_CLEAN_FINAL 2.pptx',
  'C:\Users\Auricrux\OneDrive - Future Contractors of America LLC\Copilot\Created\FCA_NVIDIA_Inception_Pitch_Deck_UNDER_5MB 1.pptx'
)

$pp = New-Object -ComObject PowerPoint.Application
$pp.Visible = -1

foreach ($f in $files) {
  if (Test-Path $f) {
    try {
      $p = $pp.Presentations.Open($f, $false, $false, $false)
      Write-Output "OPEN_OK`t$f"
      $p.Close()
    }
    catch {
      Write-Output "OPEN_FAIL`t$f`t$($_.Exception.Message)"
    }
  }
  else {
    Write-Output "MISSING`t$f"
  }
}

$pp.Quit()
