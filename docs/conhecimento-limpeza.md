# Base de conhecimento: limpeza de disco no Windows

Compilada de execuções reais em um C: de 118 GB que chegou a 0,08 GB livres, entre 6 de outubro e 14 de novembro de 2026. Cada afirmação marcada como **observado** foi medida nessa máquina. As marcadas como **hipótese** não foram confirmadas. Complementa [cobertura.md](cobertura.md), que trata da paridade com o Advanced SystemCare.

## 1. Princípios

1. **Medir antes de agir.** Registrar o espaço livre do volume e o tamanho de cada alvo. Sem número anterior não há como saber o que funcionou.
2. **Reversível primeiro.** Lixeira, link no lugar de uma pasta movida, backup verificado. Apagar de forma definitiva só o que é regenerável (cache) e mesmo assim por categoria conhecida.
3. **Nunca tocar em arquivos do usuário sem escolha explícita.** Documentos, Downloads, Vídeos e projetos entram em "mover", não em "apagar".
4. **Verificar depois de mover.** Contagem e soma de bytes por pasta; para arquivos únicos grandes, hash SHA-256.
5. **Parar o que escreve antes de copiar.** Cópia de um arquivo que outro processo ainda altera produz uma cópia desatualizada.
6. **Medir de novo.** Ganho prometido não é ganho obtido (ver seção 4).

## 2. Catálogo de alvos por risco

| Classe | Alvo | Observado | Ação |
| --- | --- | --- | --- |
| A. Regenerável, baixo risco | Pasta `%TEMP%` do usuário | 0,3 a 1 GB | Limpar o que não estiver em uso |
| A | Lixeira | 0,2 GB | Esvaziar |
| A | Cache do npm (`%LOCALAPPDATA%\npm-cache`), `npm cache clean --force` | 0,3 GB | Limpar |
| A | Cache de navegador (Chrome, Opera, Edge: `Cache`, `Code Cache`, `GPUCache`, `extensions_crx_cache`) | 1,7 GB no Chrome | Limpar com o navegador fechado. Logins, favoritos e extensões ficam. |
| A | `%APPDATA%\Code\CachedExtensionVSIXs` | 1,1 GB | Limpar. Volta ao reinstalar extensões. |
| A | Cache do Discord (`Cache`, `Code Cache`, `GPUCache`) | 0,5 GB | Limpar |
| A | Versões antigas do app Claude em `%LOCALAPPDATA%\AnthropicClaude\app-*` | 0,5 a 0,6 GB cada | Manter só a versão do processo em execução |
| B. Regenera sozinho | `%LOCALAPPDATA%\Ollama\updates_v2` (instalador de atualização) | 1,5 GB | Apagar. **Voltou duas vezes** em semanas. Vale mover o Ollama ou desinstalar. |
| B | Imagens Docker sem container (`docker image prune -a`) | 6 GB dentro do disco do Docker | Só com confirmação: o que sai é baixado de novo. |
| B | Cache de build do Docker (`docker builder prune -af`) | 1,7 GB | Seguro |
| C. Mover, não apagar | Biblioteca da Steam, vídeos, `.cache\huggingface`, projetos soltos em Downloads | 0,7 a 5,7 GB | Mover para outro disco com junction |
| C | `.codex`, `.cache\codex-runtimes` | 2,7 e 2,0 GB | Mover com junction, só com o Codex fechado |
| D. Exige administrador | `C:\Windows\SoftwareDistribution\Download`, CbsTemp, `DISM /StartComponentCleanup` | 0,6 a 5 GB | Rodar em PowerShell elevado |
| E. Não mexer | `C:\Windows\Installer` (3,8 GB), `ProgramData\Package Cache` (1,3 GB) | | Apagar quebra reparo e desinstalação de programas |
| E | `pagefile.sys`, `hiberfil.sys` | | Fora do escopo de limpeza automática |

## 3. Como mover uma pasta sem quebrar o programa

1. Confirmar que nenhum processo usa a pasta (`Get-Process`, ou tentar renomear).
2. `robocopy origem destino /E /COPY:DAT /R:1 /W:1 /MT:8`.
3. Comparar quantidade de arquivos e soma de bytes entre origem e destino.
4. Só então remover a origem e criar `mklink /J origem destino`. Junction de pasta não exige administrador.
5. Se a remoção falhar por arquivo em uso, **parar**: a pasta de origem fica pela metade. Nesse estado, informar o usuário e concluir assim que o programa fechar. (Observado: um `.xlsm` aberto no Excel deixou uma pasta de 2 GB com um único arquivo no C: e o resto no destino.)

Junction só vale para pasta. Arquivo único (um `.vhdx`, por exemplo) exige link simbólico, que pede administrador ou Modo de Desenvolvedor. A saída é mover a pasta que o contém.

## 4. Lições operacionais

- **O arquivo `.vhdx` do Docker não encolhe quando se apaga conteúdo.** Observado: `docker image prune -a` liberou 6 GB dentro do Docker e o arquivo no C: continuou com 14,48 GB. **Observado depois:** semanas mais tarde o arquivo estava em 9,8 GB e em 11,4 GB, ou seja, o Docker Desktop compacta por conta própria, de forma irregular. Para recuperar espaço de forma garantida: parar o Docker, `wsl --shutdown` e então mover ou compactar o arquivo.
- **Disco quase cheio degrada tudo.** Com menos de 1 GB livres, o Docker travou e uma cópia de milhares de arquivos pequenos ficou parada por mais de 40 minutos, quase sem uso de CPU. Primeiro abrir folga com itens da classe A, depois mover.
- **`robocopy` reserva o tamanho final do arquivo logo no início.** O tamanho no destino parece completo muito antes de a cópia terminar. Não usar o tamanho como prova de progresso nem de integridade.
- **Tamanho igual não prova cópia igual.** Observado: dois `.vhdx` de 15.518.924.800 bytes com SHA-256 diferentes. **Hipótese:** o Docker voltou a escrever no original depois da cópia. Nesse caso, a regra 5 da seção 1 vale mais que a verificação a posteriori.
- **`docker desktop stop` pode demorar ou travar com o disco cheio.** Se não responder, encerrar os processos `Docker Desktop`, `com.docker.backend`, `docker-sandbox` e executar `wsl --shutdown`.
- **Um serviço em produção cai quando o Docker cai.** Antes de parar o Docker, avisar e depois conferir cada site (HTTP 200). Erro 530 da Cloudflare significa túnel fora do ar; 502 logo após subir significa aplicação ainda no health check.
- **O sandbox e a falta de elevação bloqueiam o Windows protegido.** Remover `C:\Windows\SoftwareDistribution\Download` e rodar `DISM` falham sem administrador, mesmo com o sandbox desligado. Gerar o comando para o usuário executar em PowerShell elevado.
- **Antes de desinstalar, descobrir onde o programa está.** O Ollama parecia ocupar o C:, mas o programa e os modelos estavam no E:. Fonte: `Get-Process <nome> | Select Path`, o registro de desinstalação e variáveis como `OLLAMA_MODELS`.
- **Uma varredura recursiva completa do C: é lenta.** Medir primeiro as raízes (`AppData\Local`, `AppData\Roaming`, `Program Files`, perfil do usuário) e aprofundar só no que passa de 500 MB.

## 5. Fases de uma limpeza

1. **Folga imediata** (classe A). Meta: sair da faixa abaixo de 1 GB.
2. **Cache regenerável** (classes A e B) com os aplicativos fechados.
3. **Medição** de onde o espaço voltou a ser ocupado.
4. **Mover** pastas grandes de dados (classe C) para outro volume.
5. **Manutenção do Windows** (classe D) por comando elevado.
6. **Estruturais** (Docker, bibliotecas de jogos): com janela combinada, verificação e plano de retorno.

## 6. Lacunas no Craft que essa base aponta

O Craft hoje cobre `.tmp` antigos, atalhos quebrados e cache de código do Chrome e do Edge. Candidatos de próxima versão, em ordem de retorno e baixo risco, todos com revisão e Lixeira como já ocorre:

1. Caches da classe A com tamanho exibido: VSIX do VS Code, versões antigas de aplicativos do tipo Squirrel (`app-*`), instalador de atualização do Ollama, cache do npm.
2. Aviso de "cache que volta", com data da última limpeza e quanto voltou.
3. Relatório (somente leitura) dos `.vhdx` do Docker e do WSL com tamanho e volume, sugerindo a mudança de local em vez de agir.
4. Assistente de mover pasta com junction, com verificação de contagem e bytes e recusa a remover a origem se qualquer arquivo estiver em uso.
5. Indicação de espaço livre do volume antes e depois, gravada no histórico de cada verificação.
6. Geração do comando para os itens de classe D, para executar elevado.
