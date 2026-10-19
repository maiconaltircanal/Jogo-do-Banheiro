# Teste do Mictório

Jogo de PC (Windows) em que você entra no banheiro e escolhe em qual mictório vai mijar.
São 5 cenas, cada uma com 2 a 10 mictórios e alguns já ocupados (sorteados a cada partida).

**Regra:** nunca escolha um mictório ao lado de um que está sendo usado.
Acertou todas → veredito positivo. Errou qualquer uma → veredito negativo.

## Jogar

Baixe `dist/TesteDoMictorio.exe` e dê dois cliques. Não precisa instalar nada
(usa o .NET Framework 4 que já vem no Windows 10/11).

- Clique no mictório escolhido.
- `Enter` começa / joga de novo, `Esc` sai.

## Personalizar

Na primeira execução é criado um `config.ini` ao lado do `.exe`. Nele dá para mudar:

| Chave | O quê |
|---|---|
| `titulo`, `subtitulo` | Textos da tela inicial |
| `msg_aprovado`, `detalhe_aprovado` | Veredito quando acerta tudo |
| `msg_reprovado`, `detalhe_reprovado` | Veredito quando erra alguma |
| `cenas` | Quantidade de cenas (1 a 10) |
| `min_mictorios`, `max_mictorios` | Faixa de mictórios por cena (2 a 12) |

## Compilar

Rode `build.bat`. Ele usa o compilador C# que já vem no Windows
(`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`) e gera `dist\TesteDoMictorio.exe`.

## Estrutura

```
src/Main.cs   código do jogo (regras, geração das cenas, desenho)
build.bat     gera o executável
dist/         executável pronto
```
