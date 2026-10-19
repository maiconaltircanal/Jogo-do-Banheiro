# Testa pontuacao, ranking e sorteio das cenas usando o executavel compilado.
# Uso: powershell -ExecutionPolicy Bypass -File tools\testar-regras.ps1   (rode build.bat antes)
param([string]$Exe = "$PSScriptRoot\..\dist\JogoDoBanheiro.exe")
$null = [Reflection.Assembly]::LoadFile((Resolve-Path $Exe))
$fail = 0
function Check($name, $cond) { if ($cond) { "  ok    $name" } else { "  FALHA $name"; $script:fail++ } }

function Cena([string]$mapa, [bool]$cabine = $false) {
    # mapa: X = ocupado, _ = livre, | = divisoria entre dois mictorios. Ex.: "X|___"
    $occ = New-Object System.Collections.Generic.List[bool]; $div = New-Object System.Collections.Generic.List[bool]
    $pendingDiv = $false
    foreach ($ch in $mapa.ToCharArray()) {
        if ($ch -eq '|') { $pendingDiv = $true; continue }
        if ($occ.Count -gt 0) { $div.Add($pendingDiv) }
        $pendingDiv = $false
        $occ.Add($ch -eq 'X')
    }
    $s = New-Object JogoDoBanheiro.Scene
    $s.N = $occ.Count; $s.Occupied = $occ.ToArray(); $s.Divider = $div.ToArray(); $s.HasStall = $cabine; $s.TimeLimit = 6
    return $s
}
function Avaliar($s, $escolha, $tempo = 5) { $s.Chosen = $escolha; $s.ElapsedSeconds = $tempo; $s.Evaluate(); return $s }

"Pontuacao:"
$r = Avaliar (Cena "X___X") 2;          Check "[X___X] meio = 100 ideal" ($r.Points -eq 100 -and -not $r.Grave)
$r = Avaliar (Cena "X___X") 1;          Check "[X___X] colado = falta grave 0" ($r.Points -eq 0 -and $r.Grave)
$r = Avaliar (Cena "X____") 4;          Check "[X____] ponta oposta = 100" ($r.Points -eq 100)
$r = Avaliar (Cena "X____") 2;          Check "[X____] seguro nao ideal = 70 Passou raspando" ($r.Points -eq 70 -and $r.Comment -eq "Passou raspando")
$r = Avaliar (Cena "X|___") 1;          Check "[X|___] ao lado com divisoria = 70 Salvo pela divisoria" ($r.Points -eq 70 -and -not $r.Grave -and $r.Comment.StartsWith("Salvo pela divis"))
$r = Avaliar (Cena "X|_X") 1;           Check "[X|_X] divisoria so de um lado = grave" ($r.Grave)
$r = Avaliar (Cena "_____") 0;          Check "vazio ponta = 100" ($r.Points -eq 100)
$r = Avaliar (Cena "_____") 2;          Check "vazio meio exato = 30" ($r.Points -eq 30)
$r = Avaliar (Cena "_____") 1;          Check "vazio outro = 60" ($r.Points -eq 60)
$r = Avaliar (Cena "______") 3;         Check "vazio par, meio = 30" ($r.Points -eq 30)
$r = Avaliar (Cena "X___X" $true) 1000; Check "cabine com mictorio seguro = 40" ($r.Points -eq 40)
$r = Avaliar (Cena "X_X_X" $true) 1000; Check "cabine sem mictorio seguro = 100" ($r.Points -eq 100)
$r = Avaliar (Cena "X___X") -2;         Check "tempo esgotado = 0, sem falta grave" ($r.Points -eq 0 -and -not $r.Grave -and $r.TimedOut)
$r = Avaliar (Cena "X___X") 2 2.9;      Check "bonus decidido (2.9s de 6) = 120" ($r.Points -eq 120 -and $r.Bonus)
$r = Avaliar (Cena "X___X") 2 3.1;      Check "sem bonus (3.1s de 6)" ($r.Points -eq 100 -and -not $r.Bonus)
$r = Avaliar (Cena "X___X") 1 0.5;      Check "falta grave nao ganha bonus" ($r.Points -eq 0)

"Ranking:"
$cfg = New-Object JogoDoBanheiro.Config
function Rank([int[]]$pontos, [int]$graveIdx = -1) {
    $l = New-Object 'System.Collections.Generic.List[JogoDoBanheiro.Scene]'
    for ($i = 0; $i -lt $pontos.Count; $i++) { $s = Cena "_"; $s.Points = $pontos[$i]; $s.Grave = ($i -eq $graveIdx); $l.Add($s) }
    return [JogoDoBanheiro.Ranking]::Calcular($l, $cfg)
}
$x = Rank @(120,120,120,120,120);   Check "600 = MACHO ALFA" ($x.Nivel -eq 0 -and $x.Titulo -eq "MACHO ALFA")
$x = Rank @(120,120,100,100,100);   Check "540 = MACHO ALFA" ($x.Nivel -eq 0)
$x = Rank @(120,100,100,100,100);   Check "520 = MACHO" ($x.Nivel -eq 1)
$x = Rank @(100,100,100,70,70);     Check "440 = MACHO" ($x.Nivel -eq 1)
$x = Rank @(70,70,70,70,40);        Check "320 = EM TREINAMENTO" ($x.Nivel -eq 2)
$x = Rank @(30,30,30,30,30);        Check "150 = SUSPEITO" ($x.Nivel -eq 3)
$x = Rank @(30,30,30,30,0);         Check "120 = reprovado por pontos" ($x.Nivel -eq 4 -and $x.Mensagem -eq $cfg.DetalheReprovadoPontos)
$x = Rank @(120,120,120,120,0) 4;   Check "480 com falta grave = reprovado" ($x.Nivel -eq 4 -and $x.Grave -and $x.CenaGrave -eq 4 -and $x.Mensagem -eq $cfg.DetalheReprovado)

"Dica da cabine:"
function Partida($cenas) {
    $l = New-Object 'System.Collections.Generic.List[JogoDoBanheiro.Scene]'
    foreach ($c in $cenas) { $l.Add($c) }
    return [JogoDoBanheiro.Ranking]::Calcular($l, $cfg)
}
$soCabine = Avaliar (Cena "X_X_X" $true) 1
Check "colou quando so a cabine salvava = IgnorouCabine" ($soCabine.IgnorouCabine)
$x = Partida @($soCabine, (Avaliar (Cena "X___X") 2))
Check "reprovado por ignorar a cabine mostra a dica" ($x.Nivel -eq 4 -and $x.DicaCabine)
$x = Partida @((Avaliar (Cena "X_X_X" $true) 1000), (Avaliar (Cena "X___X") 1))
Check "reprovado por colar em cena normal NAO mostra a dica" ($x.Nivel -eq 4 -and -not $x.DicaCabine)
$x = Partida @((Avaliar (Cena "X_X_X" $true) -2), (Avaliar (Cena "_____") 2))
Check "tempo esgotado na cena da cabine NAO mostra a dica" (-not $x.DicaCabine)
$x = Partida @((Avaliar (Cena "X_X_X" $false) 1))
Check "colou sem cabine na cena NAO mostra a dica" (-not $x.DicaCabine)

"Sorteio (3000 partidas):"
$rnd = New-Object Random 123
$erros = @{}
$tamanhos = @{}
for ($t = 0; $t -lt 3000; $t++) {
    $game = [JogoDoBanheiro.Generator]::MakeGame($rnd, $cfg)
    $kinds = $game | ForEach-Object { $_.Kind.ToString() }
    if ($game.Count -ne 5) { $erros["qtd cenas"]++ }
    foreach ($k in "Vazio","Divisorias","SoCabine") { if (@($kinds | Where-Object { $_ -eq $k }).Count -ne 1) { $erros["falta $k"]++ } }
    $tempos = ($game | ForEach-Object { $_.TimeLimit }) -join ","
    if ($tempos -ne "6,5,4,3,3") { $erros["tempos $tempos"]++ }
    foreach ($s in $game) {
        $tamanhos[$s.N]++
        if ($s.N -lt 2 -or $s.N -gt 10) { $erros["tamanho"]++ }
        $s.Chosen = [JogoDoBanheiro.GameForm]::BestChoice($s); $s.ElapsedSeconds = 0; $s.Evaluate()
        if ($s.Points -ne 120) { $erros["melhor escolha nao vale 120 ($($s.Kind))"]++ }
        switch ($s.Kind.ToString()) {
            "Vazio"      { if ($s.OccupiedCount -ne 0) { $erros["vazio com gente"]++ } }
            "SoCabine"   { if ($s.HasSafeUrinal -or -not $s.HasStall) { $erros["socabine invalida"]++ } }
            "Divisorias" {
                $prot = $false; for ($i = 0; $i -lt $s.N; $i++) { if (-not $s.Occupied[$i] -and $s.NextToOccupied($i) -and -not $s.Exposed($i)) { $prot = $true } }
                if (-not $prot -or -not $s.HasSafeUrinal) { $erros["divisorias invalida"]++ }
            }
            "Normal"     { if (-not $s.HasSafeUrinal -or $s.OccupiedCount -eq 0) { $erros["normal invalida"]++ } }
        }
    }
}
if ($erros.Count -eq 0) { "  ok    nenhuma cena invalida" } else { $erros.GetEnumerator() | ForEach-Object { "  FALHA $($_.Name): $($_.Value)"; $script:fail++ } }
"  tamanhos sorteados: " + (($tamanhos.GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Name):$($_.Value)" }) -join "  ")
""
if ($fail -eq 0) { "TODOS OS TESTES PASSARAM" } else { "$fail FALHA(S)"; exit 1 }
