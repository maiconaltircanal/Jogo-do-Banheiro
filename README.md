# Jogo do Banheiro

Jogo de PC (Windows) em que você entra no banheiro e escolhe em qual mictório vai mijar.
São 5 cenas, cada uma com 2 a 10 mictórios e alguns já ocupados (sorteados a cada partida).

**Regra:** nunca escolha um mictório ao lado de um que está sendo usado.
Acertou todas → veredito positivo. Errou qualquer uma → veredito negativo.

## Instalar

1. Abra a página de [**Releases**](https://github.com/maiconaltircanal/Jogo-do-Banheiro/releases/latest).
2. Baixe `JogoDoBanheiro-Setup.exe` e execute.
3. Pronto: atalho no Menu Iniciar e na Área de Trabalho.

Não pede administrador (instala em `%LOCALAPPDATA%\JogoDoBanheiro`) e usa o .NET Framework
que já vem no Windows 10/11. Para desinstalar: **Configurações → Aplicativos → Jogo do Banheiro**.

> Se o Windows mostrar "O Windows protegeu o computador", clique em **Mais informações → Executar assim mesmo**
> (acontece com qualquer programa sem assinatura digital).

## Atualizações automáticas

Ao abrir, o jogo consulta a última Release deste repositório. Se houver versão mais nova,
pergunta se quer atualizar, baixa o instalador, confere o `sha256` publicado na Release
e reinstala sozinho, abrindo a versão nova. Sem internet, o jogo abre normalmente.

Para abrir sem verificar atualização: `JogoDoBanheiro.exe --sem-atualizacao`.

## Como jogar

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

## Desenvolvimento

Requisitos: Windows, [Git](https://git-scm.com), [GitHub CLI](https://cli.github.com) (com `gh auth login`)
e [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup`).

- `build.bat` — compila `dist\JogoDoBanheiro.exe` com o compilador C# que já vem no Windows.
- `publicar.bat 1.0.1 "O que mudou"` — grava a versão, compila, gera o instalador, faz commit/tag/push
  e cria a Release com o instalador e o `sha256`. Os jogos instalados recebem a atualização ao abrir.

```
src/Main.cs      jogo (regras, geração das cenas, desenho)
src/Updater.cs   busca e instala atualizações a partir das Releases
src/Versao.cs    número da versão (gravado pelo publicar.bat)
installer.iss    script do instalador (Inno Setup)
build.bat        compila o jogo
publicar.bat     publica uma versão nova
```
