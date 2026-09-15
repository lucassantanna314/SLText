# Especificação: Integração GitHub no SLText — Fases Restantes

> **Última atualização:** 2026-09-14 (Fase 2)
> **Status atual:** ✅ Fase 2 completa — build passing, branch button na status bar funcional
> **Arquivo anterior completo:** ver git log para histórico de mudanças

## O que JÁ ESTÁ IMPLEMENTADO

### Fase 2 — Status Bar Integration (COMPLETA) ✅

| Categoria | Status | Detalhes |
|-----------|--------|----------|
| Branch button na StatusBarComponent | ✅ | `BranchButtonBounds`, `_currentBranch`, `SetBranchName()`, `SetGitConnection()` |
| Visual indicator de conexão | ✅ | Condição `_hasGitConnection` muda cor do texto e play button |
| Event wiring no WindowManager | ✅ | `ConnectionStateChanged` atualiza `_statusBar` e `_explorer` via `InvokeOnUi()` |
| Hit-test do botão de branch | ✅ | OnLoad.cs detecta clique em `BranchButtonBounds` → chama `ToggleBranchSelector()` |
| ToggleBranchSelector (stub) | ✅ | Mostra modal com branch atual até Fase 3 ser implementada |
| ConnectToRepository | ✅ | Método limpo, verifica `.git` dir, conecta serviço, refresh explorer |
| RefreshWithGitStatus / ClearGitStatus | ✅ | FileExplorerComponent — stubs funcionais |
| InvokeOnUi helper | ✅ | Padrão `_pendingAction` para cross-thread marshalling |
| Auto-connect ao abrir folder | ✅ | Se `_lastDirectory` existe como repo, conecta automaticamente |

**Arquivos modificados na Fase 2:**
```
SLText.View/Components/StatusBarComponent.cs    — branch bounds + connection icon
SLText.View/UI/WindowManager.cs                 — service wiring, event handlers, stubs
SLText.View/UI/OnLoad.cs                        — branch button click hit-test
SLText.View/Components/FileExplorerComponent.cs — RefreshWithGitStatus/ClearGitStatus/SetSelectedFile
```

### Fase 1 — Core Services (COMPLETA) ✅

| Categoria | Status | Detalhes |
|-----------|--------|----------|
| Modelos POCO | ✅ 11 arquivos | `GitBranch`, `CommitInfo`, `ChangedFile`, `PullRequestInfo`, `MergeResult`, `RemoteBranch`, `RepositoryInfo`, `CreatePRRequest`, `IssueInfo`, `NotificationInfo`, `GitIntegrationState` |
| `IGitRepositoryClient` interface | ✅ | Contrato em `SLText.Core/Interfaces/` |
| `GitHubAuthService` | ✅ | OAuth 2.0 Device Flow com polling |
| `GitHubTokenStore` | ✅ | AES-256 encryption + file persistence |
| `GitHubToken` | ✅ | DTO com `DateTimeOffset` expiry |
| `GitNativeService` | ✅ | Wrapper LibGit2Sharp 0.30 completo (17 métodos) |
| `GitHubApiService` | ✅ | HTTP client REST v3 (repos, commits, PRs, issues, notifications) |
| `GitHubJsonContext` | ✅ | DTOs JSON para API responses |
| `GitHubIntegrationService` | ✅ | Orquestrador unificado (auth + api + git-local) |
| Dependências NuGet | ✅ | `LibGit2Sharp 0.30.0` no `.csproj` |
| Testes | ✅ 19 novos testes | Token store round-trip, models POCOs — todos passando |

**Arquivos Core criados na Fase 1:**
```
SLText.Core/Engine/Git/
├── GitHubAuthService.cs        (~200 linhas)
├── GitHubTokenStore.cs         (~170 linhas)
├── GitHubToken.cs              (~20 linhas)
├── GitNativeService.cs         (~550 linhas)
├── GitHubApiService.cs         (~340 linhas)
├── GitHubJsonContext.cs        (~410 linhas — DTOs)
├── MergeMethod.cs              (~15 linhas)
└── GitHubIntegrationService.cs (~340 linhas)

SLText.Core/Engine/Model/
├── GitBranch.cs
├── CommitInfo.cs
├── ChangedFile.cs
├── PullRequestInfo.cs
├── MergeResult.cs
├── RemoteBranch.cs
├── RepositoryInfo.cs
├── CreatePRRequest.cs
├── IssueInfo.cs
├── NotificationInfo.cs
└── GitIntegrationState.cs

SLText.Core/Interfaces/
└── IGitRepositoryClient.cs
```

---

## FASE 2 — Status Bar Integration ✅ COMPLETA

### 2.1. Branch button na StatusBarComponent ✅

**Arquivo:** `SLText.View/Components/StatusBarComponent.cs`

Implementado seguindo padrão idêntico a `PlayButtonBounds`/`SelectorBounds`:

```
┌──────────────────────────────────────────────────────────────────┐
│ 23 L │ C# │ 14pt    [▲ main ▾]   SLText.sln*      Ln 1, Col 1  │
└──────────────────────────────────────────────────────────────────┘
         ↑ Hit-test com "BranchButtonBounds.Contains(x,y)" em OnLoad
```

**Implementado:**
- ✅ Campo: `public SKRect BranchButtonBounds { get; private set; }`
- ✅ No Render: desenha `"▲ <nomeBranch> ▾"` antes do play button
- ✅ Cores dinâmicas via `_theme.Foreground` ou `_theme.Background.WithAlpha(128)` se desconectado
- ✅ Setters: `SetBranchName(string? name)` e `SetGitConnection(bool connected)`

### 2.2. Exibir branch atual conectado ao serviço ✅

O `WindowManager` agora:
1. ✅ Cria instância de `GitHubIntegrationService`
2. ✅ Wire events depois que `_statusBar` é criado (evita null reference)
3. ✅ Atualiza status bar via `InvokeOnUi()` quando `ConnectionStateChanged` dispara
4. ✅ Auto-conecta ao diretório aberto automaticamente

### 2.3. Ícone visual de estado da conexão ✅

```
Conectado: texto normal com cor do theme
Desconectado: texto cinza semi-transparente
Sem repo: botão não renderizado (bounds inativas)
```

A conexão também desativa o play button visualmente.

### 2.4. Auto-sync do branch na status bar ✅

Quando `ConnectionStateChanged` dispara, WindowManager repassa o branch novo via `InvokeOnUi()`:

```csharp
_gitHubService.ConnectionStateChanged += (_, state) =>
{
    if (_statusBar == null) return;
    InvokeOnUi(() =>
    {
        if (state.HasActiveRepo && !string.IsNullOrEmpty(state.CurrentBranch))
        {
            _statusBar.SetBranchName(state.CurrentBranch);
            _statusBar.SetGitConnection(true);
            _explorer.RefreshWithGitStatus();
        }
        else
        {
            _statusBar.SetBranchName(null);
            _statusBar.SetGitConnection(false);
            _explorer.ClearGitStatus();
        }
    });
};
```

### 2.5. Hit-test no mouse click ✅

No `OnLoad.cs`, dentro do handler de `mouse.MouseDown`:

```csharp
if (_statusBar.BranchButtonBounds.Contains(pos.X, pos.Y) && _statusBar.BranchButtonBounds.Left > 0)
{
    ToggleBranchSelector();
    return;
}
```

### 2.6. ToggleBranchSelector (stub para Fase 3) ✅

Método placeholder que abre modal informativo até a Fase 3 ser implementada:

```csharp
private void ToggleBranchSelector()
{
    string? branch = _gitHubService.State.CurrentBranch ?? "(no branch)";
    _modal.Show("Branch Selector", $"Current branch: {branch}\n\n(Fase 3 — implementar overlay completo)", null, null, null);
}
```

---

**Mudanças em WindowManager OnLoad.cs:**
- Adicionar check de clique: `if (_statusBar.BranchBounds.Contains(pos.X, pos.Y)) { ToggleBranchSelector(); return; }`

### 2.2. Exibir branch atual conectado ao serviço

O `WindowManager` precisa:
1. Criar instância de `GitHubIntegrationService`
2. Iniciar com `ConnectToRepositoryAsync(lastDirectory)` quando folder aberto
3. Passar nome do branch para `_statusBar` via evento ou setter direto
4. Atualizar após cada `SwitchBranchAsync`

### 2.3. Ícone visual de estado da conexão

Dentro do mesmo espaço onde aparece o branch name, adicionar um pequeno indicator:
- 🔵 conectado (índice azul ou círculo sólido)
- 🔴 desconectado (círculo vermelho vazio)
- ⚪ sem repo (sem indicador)

Usar `_theme` colors: ex. `_theme.LineHighlight` para connected, uma cor quente para disconnected.

### 2.4. Auto-sync do branch na status bar

Quando `GitNativeService.StateChanged` dispara, WindowManager deve repassar o branch novo:
```csharp
_gitHubService.ConnectionStateChanged += (_, state) =>
{
    if (state.CurrentBranch != null)
        _statusBar.SetBranch(state.CurrentBranch);
};
```

---

## FASE 3 — Branch Management Selector Overlay ⏱ ~6-8h

### 3.1. BranchSelectorOverlay (não-IComponent)

**Novo arquivo:** `SLText.View/Components/BranchSelectorOverlay.cs`

Segue padrão `AutocompleteComponent` / `ModalComponent`: não implementa `IComponent`.

Campos necessários:
```csharp
public class BranchSelectorOverlay
{
    public bool IsVisible { get; set; }
    private List<GitBranch> _branches = new();
    private int _selectedIndex = -1;
    private double _scrollOffset = 0;
    private readonly SKFont _font;
    private readonly SKPaint _bgPaint, _textPaint, _highlightPaint;
    private EditorTheme _theme = EditorTheme.Dark;
    
    // Bounds calculadas pelo Show()
    private SKRect _overlayRect;
}
```

Métodos públicos:
```csharp
public void Show(SKRect anchorBounds, GitHubIntegrationService service);
public void Render(SKCanvas canvas, EditorTheme theme);
public void HandleClick(float x, float y);
public void HandleKeyDown(string key);
public void Clear();
public void Update(double deltaTime); // para scroll wheel se necessário
```

### 3.2. Funcionalidades do overlay

- Lista scrollável de branches locais (`main`, `dev`, etc.) e remotos (`origin/main`)
- Ramas destacadas visualmente (cor diferente para branch atual)
- Clique seleciona → chama `_gitService.SwitchBranchAsync(name)`
- Fecha ao clicar fora ou pressionar Esc
- Tecla `/` filtra a lista por substring

### 3.3. Criar novo branch pelo selector

Se houver campo text input no topo do overlay:
- Usuário digita nome → pressione Enter → chama `_gitService.CreateBranchAsync(sourceBranch, newName)`
- Source branch default = branch corrente

### 3.4. Sync automático ao mudar branch externamente

Event handler no `WindowManager`:
```csharp
_gitHubService.ConnectionStateChanged += async (_, state) =>
{
    _branchSelector.Clear();
    if (state.HasActiveRepo && !string.IsNullOrEmpty(state.CurrentBranch))
    {
        var local = await _gitHubService.GetRecentCommitsAsync(1); // trigger refresh
        // Ou melhor: ter um método GetBranchesListAsync específico
    }
};
```

---

## FASE 4 — Commit Operations Panel ⏱ ~6-8h

### 4.1. GitHubPanelComponent (não-IComponent)

**Novo arquivo:** `SLText.View/Components/GitHubPanelComponent.cs`

Painel que fica à esquerda do editor, sobrepondo o Explorer se visível. Segue padrão CommandPalette mas mais largo (400-600px).

Abas internas:
| Tab | Conteúdo | Widget |
|-----|----------|--------|
| Commits | Lista paginada de commits | Selecionável scroll list |
| Pull Requests | PRs abertos do repo | Selecionável list |
| Changes | staged/unstaged files | Checkable list |
| Branches | branches rápidos | Selecionável list |

Cada tab é renderizada via switch dentro do `Render()`:
```csharp
private enum ActiveTab { Commits, PullRequests, Changes, Branches }
private ActiveTab _activeTab = ActiveTab.Commits;

private void RenderCommits(SKCanvas canvas, float y, float width) { ... }
private void RenderPullRequests(SKCanvas canvas, float y, float width) { ... }
private void RenderChanges(SKCanvas canvas, float y, float width) { ... }
private void RenderBranches(SKCanvas canvas, float y, float width) { ... }
```

### 4.2. Tab Commits — renderizar lista paginada

Para cada commit:
- SHA curto (7 chars) em monospace com cor diferente
- Message principal em texto normal
- Author name em itálico/cor secundária
- Relative time ("2 days ago") à direita

Ao clicar numa linha: expande detalhes (diff entre commits, author email, parent SHAs).

### 4.3. Expandir detalhes do commit

A linha clicada muda de renderização: ocupa mais altura mostrando:
- Mensagem completa (full message/body)
- Autor email
- Parent SHAs
- Arquivos modificados nesta commit

### 4.4. Cherry-pick e revert via modal de confirmação

Botões inline em cada commit expandido:
- "Cherry-pick" → abre pequeno dialog confirmando
- "Revert" → abre pequeno dialog confirmando

Estes dialogs reutilizam o pattern de `ModalComponent` mas customizado.

### 4.5. Refresh após commit/fetch

Quando `RepositorySynced` event dispara:
```csharp
_gitHubService.RepositorySynced += () => _gitHubPanel.RefreshCurrentTab();
```

### 4.6. Abrir/fechar painel via teclado

| Combinação | Ação | Handler |
|-----------|------|---------|
| `Ctrl+Shift+G` | Abrir/fechar painel GitHub | `_gitHubPanel.IsVisible = !IsVisible` |
| `Ctrl+Shift+B` | Abrir branch selector | `_branchSelector.Show(...)` |
| `Ctrl+Shift+C` | Focar tab Commits no panel | `_gitHubPanel.ActiveTab = 0; IsVisible=true` |

---

## FASE 5 — Advanced Panel Features ⏱ ~8-10h

### 5.1. Tab Pull Requests

Listar PRs abertos do repo ativo via `_gitHubService.GetOpenPullRequestsAsync()`.

Formato por item:
- Número (#) + título
- Estado colorido (verde=open, vermelho=closed)
- Autor login

Ao clicar: expande corpo do PR, base/head refs, status de CI (se API tiver).

### 5.2. Tab Changes — staged/unstaged files

Chamar `_gitHubService.GetPendingChangesAsync()` para obter lista de `ChangedFile`.

Renderização:
```
[✓] Main.cs                Modified     ← checkbox + path + status letter
[ ] Program.cs             Added
[M] styles.css             Untracked
```

Checkbox desenhado como rect vazio/preenchido. Click alterna `Staged`.

### 5.3. Stage individual file via botão inline

Cada linha de change tem um botão "+ Stage" (rect desenhado) na extremidade direita. Click chama `_gitHubService.StageFileAsync(filePath)`.

### 5.4. Visual diff básico em CommitDetailOverlay

Quando commit expandido mostra arquivos modificados, cada arquivo tem link "View Diff". Abre overlay com diff truncated por linha:

Formato:
```
@@ -10,7 +10,8 @@
-    Console.WriteLine("hello");
+    Console.WriteLine("world");
+    Console.WriteLine("updated!");
```

Linhas removidas: cor vermelha. Linhas adicionadas: cor verde. Fundo neutro para unchanged.

### 5.5. Notificações banner

No canto superior direito do editor:
- Rate limit banner: "Rate limit exceeded. Try again in X minutes" (vermelho)
- Auth expired: "Authentication expired. Please re-authenticate." (laranja)
- Sync success: "Pushed 3 commits." (verde, auto-hide 3s)

---

## FASE 6 — Merge & PR Workflow ⏱ ~6-8h

### 6.1. MergeConfirmationDialog

**Novo arquivo:** `SLText.View/Components/MergeConfirmationDialog.cs`

Modal tipo `ModalComponent` com campos extras:
- Dropdown para merge method (merge/squash/rebase)
- Botões: Confirm Merge, Cancel, View Conflicts (3 botões)

### 6.2. Fluxo fast-forward vs merge-commit detection

Antes de confirmar merge, verificar se pode ser fast-forward:
```csharp
// Se ahead == 0 e behind == 0 → fast-forward simples
// Se ahead > 0 e behind == 0 → push basta
// Se ahead > 0 e behind > 0 → fetch primeiro, depois merge
```

### 6.3. Listar conflicts encontrados

Se merge falhar, ler o output do GitNativeService:
```csharp
var result = await _gitHubService.MergeBranchAsync(source, target, method);
if (!result.Success && result.ConflictPaths.Any())
{
    // Mostrar conflicts no UI
}
```

### 6.4. Dropdown merge method

Desenhar dropdown no overlay do dialog:
```
╔══════════════════════╗
║ Merge      ▼         ║
╠══════════════════════╣
║ Merge                ║
║ Squash               ║
║ Rebase               ║
╚══════════════════════╝
```

### 6.5. Criar PR via modal

Prompt fields: title, body, draft toggle, head branch, base branch.

Simples: usar os textos existentes na status bar/current branch.

### 6.6. Merge PR via API GitHub

Endpoint: `PUT /repos/{owner}/{repo}/pulls/{number}/merge`

```csharp
var mergeResult = await _gitHubService.MergePullRequestAsync(prNumber, MergeMethod.Squash);
```

---

## FASE 7 — Polimento ⏱ ~4-6h

### 7.1. Theming completo

Garantir que TODOS os novos componentes usam `_theme`:
- Backgrounds, foregrounds, highlight colors
- Status bar colors devem bater com o tema atual
- Cores de diff (red/green for remove/add) devem ser derivadas do theme

### 7.2. Estado sincronizado via events

Todos os eventos `GitNativeService.StateChanged` → atualizar UI:
- Branch name na status bar
- Count pending push/pull na sidebar
- Colors de conexão mudam dinamicamente

### 7.3. Tratamento erros UX

- Rate limit banner: extrair header `X-RateLimit-Reset` do response
- Offline state: mostrar ícone "no connection" no status bar
- Token refresh UI: popup pedindo re-autenticação quando token expire

### 7.4. Sync periódico opcional

Poll a cada 30 segundos verificando:
- `FetchAsync()` silencioso
- Atualizar counts ahead/behind
- Só notificar usuário se realmente mudou algo

### 7.5. Documentação README atualizada

Adicionar seção no README explicando:
- Como registrar GitHub OAuth App
- Configuração inicial do token
- Shortcuts disponíveis
- Troubleshooting comum

### 7.6. Mais testes unitários nos serviços Core

Mock HttpClient/LibGit2Sharp para testar:
- `GitHubApiService` respostas corretas
- `GitNativeService` em repos simulados
- Edge cases (auth errors, network timeouts)

---

## Checklist Rápido — Resumo das Fases Restantes

| Fase | Título | Est. | Prioridade | Status |
|------|--------|------|------------|--------|
| ~~**2**~~ | ~~Status Bar Integration~~ | 4-6h | ~~🔴 Alta~~ | ✅ COMPLETA |
| **3** | Branch Selector Overlay | 6-8h | 🔴 Alta (funcionalidade central) | ⏭ Próxima |
| **4** | Commit Operations Panel | 6-8h | 🟡 Média (visualização rica) | 📋 Em fila |
| **5** | Advanced Panel Features | 8-10h | 🟡 Média (PRs, changes, diffs) | 📋 Em fila |
| **6** | Merge & PR Workflow | 6-8h | 🟢 Baixa (operação avançada) | 📋 Em fila |
| **7** | Polimento | 4-6h | 🟢 Baixa (qualidade final) | 📋 Em fila |

**Total estimado restante:** 30-40h (Fases 3–7)

---

## Próximos Passos Recomendados

Começar pela **Fase 3** — Branch Selector Overlay, que dá vida ao botão de branch na status bar:
1. Criar `BranchSelectorOverlay.cs` seguindo padrão `AutocompleteComponent` / `ModalComponent`
2. Lista scrollável com branches locais e remotos
3. Clique seleciona → chama `_gitHubService.SwitchBranchAsync(name)`
4. Campo de texto para criar novo branch
