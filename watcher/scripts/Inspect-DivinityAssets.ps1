param([string]$AssetPath)
$ErrorActionPreference='Stop'
if(-not $AssetPath){$AssetPath=Join-Path (Split-Path $PSScriptRoot -Parent) 'assets/divinity/v001'}
Add-Type -AssemblyName System.Drawing
$rows=@()
foreach($file in Get-ChildItem -LiteralPath $AssetPath -File -Filter '*.png'){
    $bitmap=[System.Drawing.Bitmap]::new($file.FullName)
    try{
        $transparent=0;$partial=0;$opaque=0;$nearOpaque=0
        # Read-only alpha sampling; no image pixels are modified.
        for($y=0;$y -lt $bitmap.Height;$y+=8){
            for($x=0;$x -lt $bitmap.Width;$x+=8){
                $alpha=$bitmap.GetPixel($x,$y).A
                if($alpha -eq 0){$transparent++}elseif($alpha -eq 255){$opaque++}else{$partial++}
                if($alpha -ge 240){$nearOpaque++}
            }
        }
        $rows+=@{file=$file.Name;sha256=(Get-FileHash -LiteralPath $file.FullName).Hash;width=$bitmap.Width;height=$bitmap.Height;pixelFormat=$bitmap.PixelFormat.ToString();sampleStep=8;transparentSamples=$transparent;partialSamples=$partial;opaqueSamples=$opaque;nearOpaqueSamples=$nearOpaque;nearOpaqueThreshold=240;topLeftAlpha=$bitmap.GetPixel(0,0).A}
    }finally{$bitmap.Dispose()}
}
if($rows.Count -eq 0){throw 'No PNG assets found.'}
$report=Join-Path $AssetPath 'asset-qa.json'
$rows | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $report -Encoding UTF8
if($rows | Where-Object {$_.transparentSamples -eq 0 -or $_.nearOpaqueSamples -eq 0}){throw 'A candidate lacks expected transparent or nearly opaque pixels; inspect the report.'}
Write-Output $report
