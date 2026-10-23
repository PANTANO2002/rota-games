# RotaGame 0.8.3 — PANT_CORE

- Corrigida a primeira criação da regra do painel no Windows: regra inexistente retorna FileNotFoundException, que antes interrompia a preparação e impedia reconhecer os dois RotaGame. Regra ausente também deixa de falhar na limpeza. Erros de permissão continuam sendo informados.
- Reconhecimento automático usa a identificação recebida pelo canal privado. Um único parceiro é salvo automaticamente; vários candidatos continuam exigindo escolha. Descoberta envia somente identidade; as medições completas vão para o parceiro selecionado.
- Ao clicar no diagrama do PC jogador, é possível escolher Direta, uma ponte disponível ou Automático. A escolha manual interrompe a comparação/monitoramento anterior, restaura sua preferência e aplica o caminho escolhido com confirmação. O destaque muda depois da aplicação.
- Modo manual aparece no estado, diagrama, relatório e painel do amigo. Não é apresentado como vencedor de um teste. ICMP sem resposta não desfaz uma escolha manual; a indisponibilidade efetiva do túnel ainda aciona a volta à direta.
- Voltar ao automático refaz a comparação objetiva. Desativar continua restaurando a preferência anterior da sessão. Alterações externas são preservadas.
- Falha do canal das medições aparece separada da disponibilidade do túnel, com orientação para permissão do Windows, resposta incompatível ou tempo esgotado.

Instale esta versão nos dois PCs. A 0.8.2 afetada pode falhar ao preparar o encerramento do painel; nesse caso use o instalador desta release. O atualizador segue embutido.

A primeira criação, leitura e remoção de uma regra isolada foram testadas no Windows real, junto de TCP no adaptador privado desta máquina. Dois perfis novos se reconheceram e trocaram uma medição ICMP real sem seleção manual, em TCP local. Isso ainda não comprova funcionamento entre os PCs reais de Acre e Cascavel. A seleção manual foi validada com rotas simuladas identificadas, sem alterar rotas do usuário.

Os números são diagnósticos ICMP e podem diferir do contador do jogo. Nenhuma redução de ping é garantida. Trocar o caminho pode reconectar a partida.