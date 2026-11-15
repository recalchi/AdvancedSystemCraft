# Cobertura funcional frente ao Advanced SystemCare 19.4

Referência local em 14 de novembro de 2026: ASC.exe informa a versão 19.4.0.210. As capturas mostram a edição Free, os modos IA e Manual e o selo Pro ao lado de Atualizar Todos. O recurso de idioma instalado, `Language/Portuguese (PT-BR).lng`, contém dezesseis categorias em `LabelNote_Scan_List.Items[0..15]`. Uma opção visível confirma a presença da interface. Sua disponibilidade após o clique e o resultado de uma correção exigem observação separada.

## Cuidado e correção

A coluna Craft descreve a implementação examinada durante o desenvolvimento da 0.6.1. A segunda coluna combina as telas e os recursos da instalação com a descrição do fabricante. [Manual da IObit](https://www.iobit.com/product-manuals/asc-help/) · [Ficha da série 19, versão 19.0.1.158](https://download.iobit.com/mediaresource/materials/asc19.pdf)

| Categorias do modo manual | Resultado esperado no original | Cobertura no Craft |
| --- | --- | --- |
| Privacidade, arquivos inúteis, atalhos | Identificar itens por categoria, revisar e corrigir os selecionados. | Parcial. Cache de código de Chrome e Edge fechados, temporários antigos e atalhos da Área de Trabalho com destino local ausente. Revisão e envio à Lixeira. |
| Registro e desfragmentação do registro | Localizar entradas elegíveis ou compactar o registro. | Pendente. A pausa reversível de uma entrada de inicialização é uma operação específica. |
| Otimizar sistema e Internet | Recomendar e aplicar ajustes conforme as configurações encontradas. | Pendente. Falta comparação das recomendações, mudanças e restauração. |
| Otimização de disco e verificação de disco | Otimização de HDD/SSD e diagnóstico com correção de erros de volume. | Parcial. O Craft solicita ao CHKDSK verificar ou reparar e ao Windows analisar ou otimizar a unidade. A equivalência do diagnóstico e das recomendações do ASC continua em aberto. |
| Proteção antivírus e firewall | Mostrar estado e oferecer gerenciamento. | Parcial. O Craft consulta os produtos registrados e o firewall por perfil e abre a Segurança do Windows. Registro de um produto, isoladamente, não comprova proteção em tempo real ativa. |
| Saúde de hardware e saúde de software | Identificar drivers e programas desatualizados. | O Craft consulta programas via WinGet. Os catálogos podem oferecer versões e cobertura diferentes. A detecção e atualização de drivers está pendente. |
| Remoção de spyware, reforço de segurança e correção de vulnerabilidades | Detectar ameaças, identificar configurações frágeis e aplicar correções de segurança. | Pendente. O inventário de antivírus do Craft identifica os fornecedores registrados. |

## Fluxos adicionais, integrações e edição Pro

| Função | Evidência no original | Implementação ou lacuna |
| --- | --- | --- |
| Seleção automática | A ficha da série 19 descreve recomendações baseadas no uso. | O modo Recomendado do Craft usa regras explícitas, entre elas o espaço livre do volume. Ainda falta comparar as seleções e correções nas mesmas condições. |
| Inicialização, aceleração e monitor | O manual descreve inicialização, Turbo Boost e monitor de recursos. | O Craft monitora CPU, memória e processos, lista discos, pausa ou restaura entradas Run do usuário e troca planos de energia do Windows com restauração da última escolha do Craft. Continuam descobertos, entre outros, atraso de inicialização, sensores e taxas de rede/disco. |
| Atualizar todos **Pro** | Selo observado na captura de Software da 19.4, corroborado pelo recurso local `btnSoftHealthPro`. | O Craft oferece lote interativo WinGet com selo Pro. Um teste de instalação com conferência da versão final é distinto de validar a consulta e a montagem do comando. |
| RAM automática, ajustes de Internet, registro e desativação de programas **Pro segundo o manual** | A instalação contém o PE `AutoRamClean.exe`. A REA identificou o artefato estático como `ev_7707016255ea84ef54b354f3ab0f0456c23c5c6e65a389d00edcdee3926aba56`. | Os efeitos dessas ferramentas no ASC Pro 19.4 continuam sem teste de execução. Medir memória e pausar uma entrada Run cobrem operações menores. |
| Proteções **Pro segundo o manual** | Antispyware, pasta protegida, antirrastreamento, proteção de impressão digital e bloqueio automático de acesso aparecem na documentação. O manual também identifica a atualização automática do próprio produto como Pro. | Equivalentes dessas funções ainda pendentes. A atualização de aplicativos instalados é separada da atualização do próprio Craft. |
| Integrações da edição de referência | Aceleração de hardware pode abrir Driver Booster. Limpeza de aplicativos e extensões usa IObit Uninstaller. Recursos de navegação e e-mail dependem de extensão. O gerenciamento da proteção do sistema pode encaminhar à Segurança do Windows. | Registrar qual programa executa a ação e qual resultado retorna. A presença de um atalho não comprova que o Craft examinou ou corrigiu o item. |
| Resgate, exclusões e histórico | O manual descreve desfazer mudanças por módulo e manter itens ignorados entre verificações. | O Craft usa a Lixeira para arquivos e restaura entradas Run pausadas. Falta demonstrar recuperação e exclusões persistentes para as demais classes. |
| Central de Ações | Inclui recomendações comerciais na edição de referência. | O projeto exclui anúncios por decisão de produto. Essa diferença é intencional. |

O manual público acompanha a evolução do produto. **Atualizar todos** tem evidência de edição na própria captura 19.4. Para os demais selos documentados, falta o confronto com uma execução da edição Pro dessa compilação. A distribuição local contém, por exemplo, `Def.dbd`, `Opt.dbd`, `Reg.dbd` e `startupSignature.db`. O inventário preserva sua existência. Formato, algoritmo, assinatura, regras efetivamente aplicadas e propriedade de cada motor continuam indeterminados. [Manual da IObit](https://www.iobit.com/product-manuals/asc-help/)

## Como demonstrar equivalência

Para cada módulo, executar original e Craft sobre cópias da mesma amostra de Windows, registrar cobertura, itens detectados, versão dos catálogos, modificações observadas, falhas e estado após desfazer. Comparar tempo, CPU e pico de memória com a mesma tarefa e as mesmas opções. Para atualizações, conferir a versão instalada depois do instalador. Para funções que requerem componentes externos, registrar a versão e a ação realizada por eles. Esta matriz documenta evidência estática e de interface; não estabelece ganho de desempenho nem paridade de detecção.
