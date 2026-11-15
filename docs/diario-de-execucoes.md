# Diário de execuções

Uma entrada por execução, da mais recente para a mais antiga. Modelo e passos em [rotina-de-execucao.md](rotina-de-execucao.md).

## 2026-11-12

Livre no C: 6,85 GB → 9,86 GB (ganho 3,0 GB). E: 198,9 GB livres. O D: não aparecia mais na lista de volumes.
Feito: varredura das raízes. Movidos para o E: com junction e verificação por contagem e bytes: `.cache\huggingface` (0,9 GB), `Documents\armorpaint` (0,3 GB), `Downloads\projects` (0,7 GB). Docker estava parado e foi religado; os dois sites responderam 200.
Voltou a crescer: instalador de atualização do Ollama em `updates_v2` (era 1,56 GB em 28/10, 0 em 12/11 após limpar).
Falhou: `Documents\FSO` (2 GB) ficou dividido porque um `.xlsm` estava aberto no Excel; cópia completa verificada no destino, origem com 1 arquivo. Conclusão pendente.
Aprendido: o Ollama já estava no E: (programa e modelos); desinstalar não liberaria nada no C:.
Pendente: concluir o `FSO`; cache de runtimes do Codex (2,0 GB, exige Codex fechado); cache do Opera (cerca de 1,8 GB, exige navegador fechado).

## 2026-10-28

Livre no C: 3,0 GB → 7,45 GB. O espaço havia caído de cerca de 10 GB para 3 GB entre as duas medições.
Feito: limpeza de `updates_v2` do Ollama, VSIX do VS Code, temp e Lixeira; remoção de duas versões antigas do app Claude. Arquivo do Docker no C: em 9,8 GB (compactado pelo próprio Docker).
Aprendido: CbsTemp e cache do Windows Update já estavam limpos (execução elevada feita pelo usuário). `Windows\Installer` e `Package Cache` ficam de fora por risco.

## 2026-10-06 e 2026-10-07 (emergência)

Livre no C: 0,08 GB → 5,0 GB; em seguida 4,5 GB, 7,7 GB e cerca de 10 GB com a limpeza de caches.
Feito: Lixeira, temp, cache do npm. Docker parado e religado várias vezes. `docker image prune -a` removeu 13 imagens (6,05 GB dentro do Docker). Caches de Chrome, Discord, Ollama e VS Code. Versões antigas do app Claude.
Falhou: duas cópias da pasta `.codex` (12.847 arquivos pequenos) travaram com o disco cheio. A cópia do `.vhdx` do Docker (14,48 GB) para o D: terminou com tamanho igual e SHA-256 diferente; a migração não foi aplicada. Sites ficaram fora do ar por longos períodos durante as tentativas.
Aprendido: ver seção 4 da base de conhecimento (`.vhdx` não encolhe sozinho, tamanho não prova cópia, parar o que escreve antes de copiar, cópia de muitos arquivos pequenos com disco cheio).
Pendente na época: migração do Docker com hash, Steam, `.codex`.
