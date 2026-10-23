# Componentes externos

- RotaGame: autoria e assinatura **PANT_CORE**. As atribuições abaixo pertencem às dependências e não alteram essa assinatura.
- Velopack 1.2.161, atualizador e empacotamento, licença MIT: [projeto e licença](https://github.com/velopack/velopack/blob/develop/LICENSE), cópia em `licenses/velopack-LICENSE.txt`.
- Microsoft.Diagnostics.Tracing.TraceEvent 4.0.0, da Microsoft, licença MIT: [projeto e licença](https://github.com/microsoft/perfview/blob/main/LICENSE.TXT).
- Runtime .NET 10 / Windows Forms na publicação independente: [licença do runtime](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT).
- Tailscale é instalado separadamente a pedido do usuário e não está embutido no executável/ZIP. Conta e uso seguem seus termos.
- Barlow Regular/Bold, Copyright 2017 The Barlow Project Authors, licença SIL OFL 1.1: [fontes oficiais](https://github.com/google/fonts/tree/main/ofl/barlow), cópia em `licenses/barlow-OFL.txt`.
- Rajdhani Regular/Bold, Copyright 2014 Indian Type Foundry, licença SIL OFL 1.1: [fontes oficiais](https://github.com/google/fonts/tree/main/ofl/rajdhani), cópia em `licenses/rajdhani-OFL.txt`.

As fontes são distribuídas sem modificações, incorporadas no executável. O app as extrai para um cache por conteúdo em `%LOCALAPPDATA%\RotaGame\Fonts` e as registra somente para seu processo, sem instalação global. O símbolo RotaGame é próprio; as famílias tipográficas são de seus respectivos autores.

As dependências transitivas são restauradas pelo NuGet; a relação resolvida aparece em obj/project.assets.json ao compilar. As fixtures de contrato não substituem a instalação/login e validação do transporte nos PCs reais.

Cópias das licenças principais estão em licenses/.
