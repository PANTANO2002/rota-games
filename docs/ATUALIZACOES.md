# Atualizações · PANT_CORE

O canal público ativo é `https://github.com/PANTANO2002/rota-games`. Credenciais GitHub não são incorporadas no programa. Quem instala só precisa de acesso HTTPS ao GitHub e aos assets das releases. O botão **Atualizações** fica no topo do RotaGame 0.9.

## Uso

- Na abertura: compara a versão instalada com a release estável mais recente, baixa e valida o pacote pelo Velopack, prepara a conexão e agenda aplicação com reinício.
- Em uso: consulta a cada 15 minutos; avisa antes de baixar/aplicar e oferece Atualizar agora / Depois.
- Depois: oculta o aviso da mesma versão nesta sessão. Uma versão posterior avisa novamente. A próxima abertura volta a verificar e aplicar automaticamente.
- Sem internet: preserva a versão. Consulta com erro não é tratada como “atualizado”.
- Antes de reiniciar: cancela a operação atual e restaura a preferência anterior pelo mesmo mecanismo de recuperação da conexão. Preparação da conta em andamento, recuperação pendente ou erro de restauração impedem o reinício.

Biblioteca, cache de fontes e recuperação ficam em `%LOCALAPPDATA%\RotaGame`, fora da pasta substituída. Não é uma garantia contra falta de energia, falha do disco ou encerramento forçado. Atualizações dependem da integridade da conta/repositório GitHub; os hashes do feed não são uma assinatura independente da release. Não há Authenticode configurado.

## Gerar e publicar uma versão

1. Use Windows x64, SDK .NET 10 e `dotnet tool install --global vpk --version 1.2.161`.
2. Atualize Version no projeto, a versão dos relatórios e as notas em RELEASE-NOTES.md. Não reutilize números de versões publicadas.
3. Execute os testes: `dotnet run --project tests/RotaGame.Tests -c Release -- --integration`.
4. Execute `./pack.ps1 -Version 0.9.0`. O parâmetro deve coincidir com a versão do código.
5. Publique uma release estável com tag `v0.9.0` no repositório configurado. Inclua exatamente `RotaGame-Setup.exe`, `RotaGame-win-Portable.zip`, `RotaGame-0.9.0-full.nupkg`, `releases.win.json` e `SHA256SUMS.txt` de Releases/.
6. Confira o download público dos cinco assets. O feed aponta para o nupkg; trocar apenas o EXE não publica uma atualização.

O instalador público é um bootstrap .NET Framework com manifesto requireAdministrator. Ele confere o SHA256 do Setup Velopack incorporado e inicia a instalação com interface normal. Não contorna a confirmação UAC e não concede privilégios permanentes. O Setup interno e assets.win.json são intermediários: não os envie à release.

O pacote portátil completo preserva Update.exe e os metadados de versão necessários. Mover só RotaGame.exe quebra esse formato. Usuários de versões anteriores a 0.7 precisam fazer uma migração manual.

## Validação reproduzível

- `RotaGame.exe --update-smoke PASTA`: regras de abertura/aviso/adiamento, erros, cancelamento, preparação antes de reiniciar e capturas da interface, com fonte de versões controlada.
- `RotaGame.exe --update-package-smoke Releases PASTA`: descobre/baixa o pacote real e verifica rejeição de uma cópia adulterada em diretório isolado. Não instala no perfil do usuário.
- Integração isolada: empacote uma versão anterior de teste, extraia o ZIP portátil em work/, e execute `RotaGame.exe --update-integration CAMINHO_RELEASES CAMINHO_MARCADOR`. O atualizador aplica a nova versão e reinicia para registrar o resultado. Esse modo usa apenas um feed local explícito e não altera rotas, contas ou a instalação normal.

Documentação do componente: [integração](https://docs.velopack.io/integrating/overview), [empacotamento](https://docs.velopack.io/packaging/overview) e [testes](https://docs.velopack.io/integrating/testing).
