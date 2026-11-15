# Rotina de execução de uma limpeza

Roteiro para toda execução, manual ou assistida. O último passo alimenta o [diário](diario-de-execucoes.md) e a [base de conhecimento](conhecimento-limpeza.md), então cada execução melhora a seguinte.

## Antes

1. **Registrar o ponto de partida.** Espaço livre de cada volume fixo, data, e o que está em uso (Docker, Codex, navegadores, Office).
2. **Escolher o nível de urgência.**
   - Abaixo de 1 GB livres: só classe A da base de conhecimento, sem copiar nada.
   - De 1 a 10 GB: classes A e B, depois mover (classe C).
   - Acima de 10 GB: manutenção e estruturais com calma.
3. **Medir as raízes** antes dos detalhes: perfil do usuário, `AppData\Local`, `AppData\Roaming`, `Program Files`, `ProgramData`, Windows. Aprofundar só o que passa de 500 MB.
4. **Comparar com o diário.** Se um alvo da classe B já voltou antes, tratar a causa (mover, desinstalar, desligar a atualização automática) e não só apagar.

## Durante

5. **Fechar o que escreve** nos alvos: navegador, Docker, Codex, Excel com pasta de trabalho no alvo.
6. **Agir na ordem de risco**: A, B, C, D, E (não mexer). Mostrar ao usuário o que vai sair antes de qualquer remoção irreversível.
7. **Mover com verificação** (seção 3 da base): copiar, comparar contagem e bytes, remover a origem, criar junction. Se a remoção falhar, parar e informar.
8. **Itens de administrador** viram comando pronto para o usuário executar elevado. Não tentar contornar o bloqueio.

## Depois

9. **Conferir serviços** que dependem do que foi tocado (sites atrás do Docker: HTTP 200 em cada um).
10. **Medir de novo** o espaço livre e calcular o ganho por item. Ganho prometido que não apareceu no volume (como o `.vhdx`) é anotado como tal.
11. **Gravar no diário**: data, espaço antes e depois, o que foi feito, o que voltou a crescer, o que falhou e por quê.
12. **Atualizar a base de conhecimento** quando algo novo foi aprendido: alvo novo e sua classe, comando que falhou, hipótese confirmada ou derrubada. Marcar como **observado** ou **hipótese**.
13. **Revisar a lista de lacunas** (seção 6 da base): se o que foi feito à mão se repetiu, vira candidato de funcionalidade do Craft.

## Modelo de entrada do diário

~~~text
## AAAA-MM-DD
Livre no C: X GB → Y GB (ganho Z GB). Livre nos demais: ...
Feito: ...
Voltou a crescer / falhou: ...
Aprendido: ...
Pendente: ...
~~~
