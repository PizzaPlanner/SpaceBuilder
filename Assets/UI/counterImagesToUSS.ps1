#Requires -Version 7.0

$counterfile = "$PSScriptRoot/Counter.png".replace("\","/")
$counterfileRelativeToAssets = $counterfile.Split("/Assets/")[-1]
$metadata = Get-Content "${counterfile}.meta"
$targetFile = "$PSScriptRoot/coordinatesegments.uss"
$counterfileProjectUrl = "project://database/Assets/${counterfileRelativeToAssets}"
Write-Host $counterfile
Write-Host $counterfileProjectUrl
Write-Host $counterfileRelativeToAssets

$guid = $metadata | where {$_ -match "guid: (.+)"} | foreach {$matches[1]}
$filtered = $metadata | where {$_.Contains("name: ") -or $_.Contains("internalID: ")}

$data = $filtered | foreach {
    if($_.contains("Counter_")) {
        $current = $_.Split("Counter_")[-1]
        ".segment-$current {"
    } elseif($_.contains("internalID") -and $current) {
        "    background-image: url('${counterfileProjectUrl}?fileID=$($_.split(': ')[-1])&guid=${guid}&type=3#Counter_${current}');`n}`n"
        $current = $null
    }
}

$data > $targetFile