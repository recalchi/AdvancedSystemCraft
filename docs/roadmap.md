# Roadmap até a 1.0

Este roadmap organiza as atualizações do Advanced System Craft até a primeira versão definitiva. Um marco só é concluído quando as funções listadas estão implementadas, validadas e documentadas. Versões intermediárias continuam como pré-lançamentos.

## Situação atual

**Versão funcional mais recente:** 0.6.4.

O Craft já cobre revisão e correção reversível de arquivos selecionados, atualizações via WinGet, diagnósticos de proteção, inicialização, monitoramento de recursos, planos de energia, manutenção de discos delegada ao Windows e histórico local. A matriz detalhada está em [cobertura.md](cobertura.md).

## Marcos

| Marco | Versão alvo | Resultado esperado | Critério de saída |
| --- | --- | --- | --- |
| Confiabilidade da base | 0.7 | Fluxos já entregues com histórico, exclusões e recuperação coerentes. | Todos os testes locais e Windows hospedado passam. Cada operação mutável informa resultado e como desfazer. |
| Cuidado e limpeza | 0.8 | Categorias de limpeza próprias com critérios explícitos, prévia por item e exclusões persistentes. | Amostras controladas comprovam detectar, ignorar, corrigir e recuperar cada categoria suportada. Cobertura e limites aparecem na interface e na documentação. |
| Desempenho e manutenção | 0.9 | Inicialização, energia, memória, discos e atualizações consolidados em fluxos verificáveis. | Operações delegadas ao Windows registram comando, resultado, necessidade de elevação e reinicialização quando aplicável. Atualização individual é comprovada em VM descartável. |
| Proteção e privacidade | 0.9.x | Diagnóstico acionável do Windows Defender, firewall, Windows Update e dados locais de navegador suportados. | Estado real, indisponibilidade e ação tomada ficam distinguíveis. Nenhuma função promete bloquear ameaças ou anúncios sem um motor que o faça. |
| Equivalência medida | RC | Comparação por cenários entre Craft e a instalação de referência. | Para cada categoria anunciada, existe cenário de amostra, cobertura examinada, resultado, custo de tempo/CPU/memória e estado após desfazer. Diferenças permanecem documentadas. |
| Preparação de distribuição | RC | Empacotamento, identidade de publicação e instalação documentados. | Metadados de publicador, manifesto, hashes, notas de versão e instruções de atualização estão corretos. Assinatura Authenticode entra quando houver certificado disponível. |
| Primeira versão definitiva | 1.0 | Conjunto de funcionalidades suportadas estável e verificável. | Sem falhas conhecidas de perda de dados nos fluxos suportados, validação em Windows hospedado passa, guia de limites atualizado e revisão humana do pacote concluída. |

## Próximas etapas priorizadas

1. Ampliar as amostras de limpeza e recuperação para validar as categorias já visíveis no Cuidado.
2. Finalizar a integração dos resultados de Defender, Windows Update e manutenção de disco no histórico de cuidado.
3. Medir os cenários equivalentes do ASC e do Craft antes de declarar paridade funcional ou de desempenho.
4. Preparar a cadeia de assinatura Authenticode quando um certificado de código estiver disponível.

## Formato obrigatório dos próximos relatórios

Todo relatório de atualização deve terminar com este bloco curto:

```text
Onde estamos: versão/marco atual e validação mais recente.
Próxima etapa: a próxima entrega verificável.
Risco ou limite: somente o que ainda impede a 1.0 ou muda o comportamento anunciado.
```

O relatório também deve separar o que foi observado, o que foi testado e o que ainda é pendente. Um recurso só entra como concluído no roadmap quando atende ao critério de saída do marco correspondente.
