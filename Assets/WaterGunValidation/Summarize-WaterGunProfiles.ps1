param([string[]]$RunNames=@('demo-gpu-phase5-20261003','fixture-gpu-phase5-20261003','demo-gpu-repeat-phase5-20261003','fixture-gpu-repeat-phase5-20261003'))
$ErrorActionPreference='Stop'
function Stats($values){
 $ordered=@($values | Sort-Object)
 if($ordered.Count -eq 0){return $null}
 $middle=[int][Math]::Floor($ordered.Count/2)
 $median=if($ordered.Count%2){$ordered[$middle]}else{($ordered[$middle-1]+$ordered[$middle])/2}
 [ordered]@{count=$ordered.Count;median=$median;p95=$ordered[[Math]::Ceiling($ordered.Count*.95)-1];max=$ordered[-1];mean=($ordered | Measure-Object -Average).Average}
}
$runs=foreach($name in $RunNames){
 $data=Get-Content -LiteralPath (Join-Path '.codex-artifacts/WaterGun/profiles' "$name/samples.json") -Raw | ConvertFrom-Json
 $scenarios=foreach($group in ($data.rows | Group-Object mode)){
  $metrics=[ordered]@{}
  for($i=0;$i -lt $data.metricNames.Count;$i++){
   $unit=if($data.metricNames[$i] -eq 'GC Allocated In Frame'){1}else{1000000}
   $metrics[$data.metricNames[$i]]=Stats @($group.Group | ForEach-Object {$_.metrics[$i]/$unit})
  }
  $gpuRows=@($group.Group | Where-Object {$_.gpuNs -gt 0} | Group-Object gpuFrame | ForEach-Object {$_.Group[0]})
  [ordered]@{mode=$group.Name;frames=$group.Count;firingFrames=@($group.Group | Where-Object firing).Count;deposits=@($group.Group | Where-Object {$_.deposits -gt 0}).Count;
   pathCounts=@($group.Group | Group-Object pathCount | ForEach-Object {@{path=[int]$_.Name;frames=$_.Count}});
   maxGeometryRefresh=($group.Group.geometryRefresh | Measure-Object -Maximum).Maximum;
   elapsedMs=Stats $group.Group.elapsedMs;over16ms=@($group.Group | Where-Object {$_.elapsedMs -gt 16.667}).Count;
   cpu=$metrics;gpuMs=Stats @($gpuRows | ForEach-Object {$_.gpuNs/1000000})}
 }
 [ordered]@{name=$name;prefab=$data.PrefabPath;cpu=$data.cpu;gpu=$data.gpu;screen=$data.screen;graphicsJobs=$data.graphicsJobs;uniqueGpuFrames=$data.uniqueGpuFrames;availability=$data.availability;scenarios=$scenarios}
}
[ordered]@{date='2026-10-03';scope='Unity Editor runtime driver; Warudo asset scan excluded';units='ms except GC bytes';quantile='nearest rank p95; even median average';runs=$runs} | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath '.codex-artifacts/WaterGun/phase5-performance.json' -Encoding UTF8
$runs | ForEach-Object {$name=$_.name; $_.scenarios | ForEach-Object {[pscustomobject]@{run=$name;mode=$_.mode;tick=$_.cpu['WaterGun.Tick'].median;tickP95=$_.cpu['WaterGun.Tick'].p95;gpu=$_.gpuMs.median;gpuP95=$_.gpuMs.p95;fire=$_.firingFrames}}} | Format-Table
