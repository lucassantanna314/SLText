# Especificação: Integração GitHub no SLText

> **Última atualização:** 2026-09-14  
> **Autoridade:** Especificação aprovada após análise arquitetural profunda do codebase.

## Visão Geral

Implementar integração completa com GitHub dentro do editor SLText, permitindo gerenciamento de repositórios, commits, branches, PRs e merge diretamente na UI.

---

## 1. Arquitetura Confirmada (Analise do Codebase)

**SlText NÃO usa nenhuma UI framework** — renderização 100% custom via SkiaSharp + Silk.NET (GLFW).

| Camada | Arquivo / Localização | Tecnologia | Responsabilidade |
|--------|----------------------|------------|------------------|
| Rendering | `SLText.View/UI/Render.cs` | `SKCanvas`, `SKPaint`, `SKFont` | Pipeline frame-a-frame desenha cada componente em ordem Z |
| Componentes | `SLText.View/Abstractions/IComponent.cs` | `IComponent` interface | Contrato: `Bounds`, `Render(SKCanvas)`, `Update(double)` |
| Modais | `SLText.View/Components/ModalComponent.cs` | Não-implementa `IComponent` | Diálogo centralizado com sim/não/cancel, hit-testing próprio |
| Input Mouse | `SLText.View/UI/Input/MouseHandler.cs` | `MouseButton`, position floats | Roteia clicks baseado em `Bounds.Contains()` |
| Input Key | `SLText.Core/Engine/InputHandler.cs` | `ICommand` execute/undo | Mapeamento `(ctrl, shift, key)` → `Func<ICommand>` |
| Serviços View | `SLText.View.Services/` | Singleton pattern | `RunService`, `SettingsService`, `NativeDialogService` |
| Interfaces Core | `SLText.Core/Interfaces/` | Contratos POCO | `ICommand`, `IDialogService`, `IZoomable` |
| Commands Core | `SLText.Core/Commands/` | Implementações `ICommand` | `TypingCommand`, `SaveFileCommand`, etc. |
| Modelos Core | `SLText.Core.Engine.Model/` | POCOs dados | `TabInfo`, `RunConfiguration`, `FileNode` |
| LSP Service | `SLText.Core.Engine/LSP/LspService.cs` | Microsoft.CodeAnalysis | Pattern referência para async services (lock/gate, events) |
| Terminal Service | `SLText.Core.Engine/TerminalService.cs` | `System.Diagnostics.Process` | Async process spawn, events OnDataReceived |

### Componentes atuais (quem implementa `IComponent`):

| Componente | Implements `IComponent`? | Local | Função |
|---|---|---|---|
| EditorComponent | Sim | `Canvas/EditorComponent.cs` | Canvas principal de texto |
| StatusBarComponent | Sim | `StatusBarComponent.cs` | Barra inferior |
| TerminalComponent | Sim | `TerminalComponent.cs` | Terminal com tabs |
| FileExplorerComponent | Sim | `FileExplorerComponent.cs` | Sidebar árvore de arquivos |
| CommandPaletteComponent | Sim | `CommandPaletteComponent.cs` | Palette Ctrl+Shift+P |
| ContextMenuComponent | Sim | `ContextMenuComponent.cs` | Menu direito |
| TabComponent | Sim | `TabComponent.cs` | Tab strip |
| ModalComponent | **Não** | `ModalComponent.cs` | Overlay centralizado, hit-testing próprio |
| SearchComponent | **Não** | `SearchComponent.cs` | Find-in-file box |
| AutocompleteComponent | **Não** | `AutocompleteComponent.cs` | Code completion dropdown |
| SignatureHelpComponent | **Não** | `SignatureHelpComponent.cs` | Method signature tooltip |

### Padrões essenciais a seguir:

1. **Hit-testing manual por bounds**: Novo botão adiciona campo `SKRect`, check em `WindowManager.OnLoad` via `.Contains(x, y)`. Exemplo: `PlayButtonBounds`, `SelectorBounds`.
2. **Eventos delegate**: InputHandler exporta `OnXxxRequested += callback` para ações nível alto. Novos inputs mapeados em InputHandler.
3. **Async pattern (LSP style)**: Usar `_gate = new SemaphoreSlim(1,1)` para thread safety, async-await, eventos `OnStateChanged`, `Task.Run` para ops pesadas.
4. **Theming-aware**: Todos componentes têm campo `_theme` e método `ApplyTheme(EditorTheme theme)`.
5. **Non-IComponent overlays**: Popups como Modal/Autocomplete são campos diretos no WindowManager, renderizados manualmente quando `IsVisible=true`.
6. **Dependência direction**: View → Core. Serviços API podem ir em Core se puros (sem UI), ou View se mantêm estado de UI.
7. **Zero HttpClient hoje**: Nunca houve chamada HTTP no projeto. Adicionar `System.Net.Http.Json` ao csproj se necessário (embora .NET 10 tenha HttpClient nativo sem pacote).

---

## 2. Componentes UI Necessários

### 2.1. Novo item na StatusBar — "GitHub / Branch"

**Arquivo:** `StatusBarComponent.cs` (editar)

Padrão idêntico a `PlayButtonBounds`/`SelectorBounds`: definir nova `SKRect _branchBounds`, desenhar texto + seta no `Render()`.

```
┌──────────────────────────────────────────────────────────────────┐
│ 23 L │ C# │ 14pt    [▲ main ▾]   SLText.sln*      Ln 1, Col 1  │
└──────────────────────────────────────────────────────────────────┘
         ↑ Hit-test com "_branchBounds.Contains(x,y)" em OnLoad
```

**Mudanças em StatusBarComponent.cs:**
- Campo: `public SKRect BranchBounds { get; private set; }`
- No Render: desenhar `"▲ <nomeBranch> ▾"` antes do play button
- `ApplyTheme(EditorTheme)` — usar `_theme` para cores

**Mudanças em WindowManager OnLoad.cs:**
- Adicionar check de clique: `if (_statusBar.BranchBounds.Contains(pos.X, pos.Y)) { ToggleBranchSelector(); return; }`

### 2.2. BranchSelectorOverlay (não-IComponent, estilo Modal/Autocomplete)

**Novo arquivo:** `SLText.View/Components/BranchSelectorOverlay.cs`

Não implementa `IComponent` — é um overlay popup como `AutocompleteComponent` e `ModalComponent`. Segue o padrão existente:
- Campo `bool IsVisible`
- Método `Show(SKRect anchorBounds, ...)` para posicionar abaixo do botão na status bar
- Métodos próprios `RenderButtons()` ou `DrawSelectableList()` usando canvas local
- Hit-testing interno por posição Y dentro da lista renderizada

#### Funcionalidades:
- Lista scrollável de branches locais (`main`, `dev`, etc.) e remotos (`origin/main`)
- Ramas destacadas visualmente (cor diferente para branch atual)
- Clique seleciona → chama `_gitService.CheckoutBranchAsync(name)`
- Fecha ao clicar fora ou pressionar Esc

### 2.3. GitHubPanelOverlay — Painel lateral de GitHub

**Novo arquivo:** `SLText.View/Components/GitHubPanelComponent.cs`

Similar a `FileExplorerComponent`: implementa `IComponent` quando persistente, mas como painel tab-based temporário, segue o padrão **não-IComponent overlay** (como CommandPalette). Posiciona-se à esquerda do editor, sobrepondo explorer se visível.

#### Abas internas:
| Tab | Conteúdo | Widget |
|-----|----------|--------|
| Commits | Lista paginada de commits | Selecionável scroll list |
| Pull Requests | PRs abertos do repo | Selecionável list |
| Changes | staged/unstaged files | Checkable list |
| Branches | branches rápidos | Selecionável list |

#### Cada item da lista:
- Linha horizontal com texto truncado + ellipsis
- Ao clicar: expande detalhes (segunda camada de renderização no mesmo componente)
- Botões de ação inline (checkout, stage, merge) desenhados como rects clicáveis

### 2.4. CommitDetailOverlay — Visualização expandida de commit

**Novo arquivo:** `SLText.View/Components/CommitDetailOverlay.cs`

Popup que aparece ao clicar num commit (padrão não-IComponent). Mostra diff entre commits, autor, data, SHA completo. Usa layout horizontal com scroll X + scroll Y. Similar ao autocomplete dropdown em estrutura, porém mais rico.

### 2.5. MergeConfirmationDialog

**Novo arquivo:** `SLText.View/Components/MergeConfirmationDialog.cs`

Fusão do padrão `ModalComponent` + informações específicas de merge:
- Exibe conflicts encontrados (se houver) com trechos de código coloridos
- Dropdown para merge method (merge/squash/rebase)
- Botões: Confirm Merge, Cancel, View Conflicts (3 botões como ModalComponent)

---

## 3. Camada de Serviço / API

### 3.1. Arquitetura de Serviços — Decisão de Localização

O codebase tem dois padrões para services:
- **Core**: lógica pura sem dependência de UI (ex: `LspService` tem state de UI mas vive em Core porque usa Microsoft.CodeAnalysis)
- **View**: serviços que dependem de estado visual (ex: `RunService` gerencia `_activeConfiguration` que está no WindowManager)

Decisão para GitHub:

| Serviço | Local | Motivo |
|---------|-------|--------|
| Modelos (`CommitInfo`, `GitBranch`, etc.) | `SLText.Core.Engine.Model/` | POCOs puros, zero UI |
| `IGitRepositoryClient` interface | `SLText.Core.Interfaces/` | Contrato testável |
| `GitNativeService` (LibGit2Sharp wrapper) | `SLText.Core.Engine/Git/` | Lógica git = lógica de negócio |
| `GitHubAuthService` | `SLText.Core.Engine/Git/` | Auth flow = lógica de negócio, sem UI |
| `GitHubApiService` (REST client) | `SLText.Core.Engine/Git/` | Chamadas HTTP = lógica de negócio |
| `GitHubIntegrationService` (orchestrator) | `SLText.Core.Engine/Git/` | Coordena auth+api+git-local |
| Estado UI de GitHub | View-side field no WindowManager | `IsGithubPanelVisible`, current selection state |

### 3.2. GitHubAuthService

**Novo arquivo:** `SLText.Core.Engine/Git/GitHubAuthService.cs`

OAuth 2.0 via Device Flow (sem browser). Padrão async conforme LSP Service pattern.

```csharp
public class GitHubAuthService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private const string ClientId = "<registrado-no-github>";
    private const string TokenUrl = "https://github.com/login/oauth/access_token";
    private const string DeviceCodeUrl = "https://github.com/login/device/code";
    
    public GitHubToken? CurrentToken { get; private set; }
    public event EventHandler<bool>? AuthStateChanged; // authenticated/disauthenticated
    
    public async Task AuthenticateViaDeviceFlowAsync(DeviceFlowCallback callback);
    public async Task RefreshAccessTokenAsync();
    public void ClearTokens();
}

public class GitHubToken
{
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}

// Callback usado pela View para mostrar o device code ao usuário
public delegate void DeviceFlowCallback(string userCode, Uri verificationUri);
```

**Fluxo completo:**
1. `AuthenticateViaDeviceFlowAsync()` chama POST `/login/oauth/device/code`
2. Recebe `device_code`, `user_code` (8 chars), `verification_uri`
3. Emite evento `AuthStateChanged(false)` e dispara callback com dados para View exibir
4. View mostra modal/popup: "Enter code: AB12 CD34 at https://github.com/login/device"
5. App faz polling POST `/login/oauth/access_token` a cada 5 segundos
6. GitHub responde com token ou "authorization_pending"
7. Ao sucesso: armazena, emite `AuthStateChanged(true)`

**Armazenamento seguro de tokens** (cross-platform):
- Linux: usar `libsecret` via NuGet `SecretStorage` (GNOME Keyring / KDE Wallet)
- Fallback: arquivo criptografado AES-256 em `~/.config/sltext/github-tokens.enc`
- Chave derivada de máquina única (usar `MachineKey` do .NET)

### 3.3. GitNativeService

**Novo arquivo:** `SLText.Core.Engine/Git/GitNativeService.cs`

Wrapper sobre **LibGit2Sharp** seguindo padrão `LspService._gate` para thread safety.

```csharp
public class GitNativeService : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Repository? _repo;
    private readonly string _repositoryPath;
    
    // Estado público reativo
    public string? CurrentBranchName { get; private set; }
    public bool HasUncommittedChanges { get; private set; }
    public int LocalAheadCount { get; private set; }
    public int LocalBehindCount { get; private set; }
    public List<string> UntrackedFiles { get; private set; } = new();
    
    public event EventHandler<GitStateChangedArgs>? StateChanged;
    
    public void OpenRepository(string path);
    public Task<List<GITBranch>> ListLocalBranchesAsync();
    public Task<List<RemoteBranch>> ListRemoteBranchesAsync(string remote = "origin");
    public Task SwitchBranchAsync(string branchName);
    public Task CreateBranchAsync(string sourceBranch, string newBranchName);
    public Task PushAsync(string branchName, bool force = false);
    public Task FetchAsync();
    public Task<PullResult> PullAsync();
    public Task CommitAsync(string message, IEnumerable<string>? files = null);
    public Task CherryPickAsync(string sourceSha);
    public Task RevertAsync(string sha);
    public Task<List<ChangedFile>> GetStatusAsync();
    public Task StageFileAsync(string filePath);
    public Task StageAllAsync();
}
```

**Métodos auxiliares de conveniência:**
```csharp
public async Task<List<CommitInfo>> GetLogAsync(int count = 50, string branch = "HEAD")
{
    await _gate.WaitAsync();
    try {
        var logs = _repo?.Head.Tip.Children.Take(count);
        // Mapear para CommitInfo DTOs
    } finally { _gate.Release(); }
}
```

### 3.4. GitHubApiService

**Novo arquivo:** `SLText.Core.Engine/Git/GitHubApiService.cs`

HTTP REST client para API do GitHub (v4 GraphQL opcional depois). Seguir padrão `LspService` com `HttpClient` shared instance, `_gate`, eventos de progresso.

```csharp
public class GitHubApiService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HttpClient _httpClient;
    private readonly GitHubAuthService _auth;
    
    public event EventHandler<ApiResponseEventArgs>? ApiResponseReceived;
    
    public GitHubApiService(GitHubAuthService authService, HttpClient httpClient) { ... }
    
    // Repositórios
    public Task<RepositoryInfo> GetRepositoryInfoAsync();
    public Task<List<RemoteBranch>> GetRemoteBranchesAsync();
    
    // Commits via API (complemento ao git-local)
    public Task<List<CommitInfo>> GetRemoteLogAsync(int page = 1, int perPage = 30);
    public Task<CommitInfo?> GetCommitAsync(string sha);
    
    // Pull Requests
    public Task<List<PullRequestInfo>> ListOpenPullRequestsAsync();
    public Task<PullRequestInfo> CreatePullRequestAsync(CreatePRRequest request);
    public Task<MergeResult> MergePullRequestAsync(int prNumber, MergeMethod method);
    public Task<PullRequestInfo?> GetPullRequestAsync(int number);
    
    // Issues (futuro)
    public Task<List<IssueInfo>> ListIssuesAsync(IssueState state = IssueState.Open);
    
    // Notifications
    public Task<List<NotificationInfo>> GetNotificationsAsync();
}
```

**MergeMethod enum:**
```csharp
public enum MergeMethod { Merge, Squash, Rebase }
```

### 3.5. GitHubIntegrationService (Orquestrador)

**Novo arquivo:** `SLText.Core.Engine/Git/GitHubIntegrationService.cs`

Única classe pública que a View consulta. Soma os três subservices.

```csharp
public class GitHubIntegrationService
{
    private readonly GitHubAuthService _auth;
    private readonly GitHubApiService _api;
    private readonly GitNativeService _git;
    
    public GitHubIntegrationState State { get; private set; }
    
    public event EventHandler<GitHubIntegrationState>? ConnectionStateChanged;
    public event EventHandler<RepositorySyncEventArgs>? RepositorySynced;
    
    public async Task ConnectToRepositoryAsync(string repositoryPath);
    public async Task AuthenticateAsync();
    public async Task DisconnectAsync();
    public async Task<List<CommitInfo>> GetRecentCommitsAsync(int count = 50);
    public Task SwitchBranchAsync(string branchName);
    public Task PushAsync();
    public Task PullAsync();
    public Task<List<ChangedFile>> GetPendingChangesAsync();
    public Task<List<PullRequestInfo>> GetOpenPullRequestsAsync();
    public Task<MergeResult> MergeBranchAsync(string sourceBranch, string targetBranch, MergeMethod method);
    public Task<PullRequestInfo> CreatePullRequestAsync(string title, string body, string head, string baseBranch);
    public Task CherryPickAsync(string sha, string targetBranch);
}
```

### 3.6. GitHubTokenStore (Persistência)

**Novo arquivo:** `SLText.Core.Engine/Git/GitHubTokenStore.cs`

Interface mínima para persistir tokens. Implementação concreta pode alternar entre keyring e file-based dependendo do platform.

```csharp
public static class GitHubTokenStore
{
    public static async Task SaveAsync(GitHubToken token);
    public static Task<GitHubToken?> LoadAsync();
    public static void Clear();
}
```

---

## 4. Modelos de Dados

**Novos arquivos em `SLText.Core.Engine.Model/`:**

```
SLText.Core.Engine.Model/
├── GitBranch.cs          (Name, IsLocal, RemoteName?, IsCurrent, UpstreamBranch?)
├── CommitInfo.cs         (Sha, ShortSha, Message, AuthorName, AuthorEmail, CommittedAt, TreeId)
├── ChangedFile.cs        (Path, Status: Added|Modified|Deleted|Rename|Unmerged, Staged: bool)
├── PullRequestInfo.cs    (Number, Title, Body?, State: Open|Closed, HeadBranch, BaseBranch, AuthorLogin, CreatedAt, MergedAt, FilesChanged, Additions, Deletions)
├── MergeResult.cs        (Success: bool, FastForward: bool, ConflictPaths: string[], ErrorMessage?)
├── RemoteBranch.cs       (Name, Url, IsTrackingLocal: bool, LocalBranchName?)
├── RepositoryInfo.cs     (FullName, Description, DefaultBranch, IsPrivate, PushUrl, CloneUrl)
├── CreatePRRequest.cs    (Title, Body, Head, Base, Draft: bool)
├── IssueInfo.cs          (Number, Title, State, AuthorLogin, Labels[], CreatedAt)
├── NotificationInfo.cs   (Id, Reason, Unread: bool, RepositoryName, SubjectType, SubjectTitle)
└── GitIntegrationState.cs (IsAuthenticated: bool, HasActiveRepo: bool, RepoPath, CurrentBranch, HasPendingPush, HasPendingPull)
```

### Padrão POCO — exemplo mínimo (`CommitInfo.cs`):

```csharp
namespace SLText.Core.Engine.Model;

public class CommitInfo
{
    public string Sha { get; set; } = "";
    public string ShortSha => Sha[..7];
    public string Message { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public string AuthorEmail { get; set; } = "";
    public DateTime CommittedAt { get; set; }
    public string? TreeId { get; set; }
}
```

---

## 5. Integração com WindowManager & InputHandler

### 5.1. Novo campo no WindowManager.cs

```csharp
// Serviços Core
private GitHubIntegrationService _gitHubService = new();

// Componentes UI overlays (não-IComponent)
private BranchSelectorOverlay _branchSelector = new();
private GitHubPanelComponent _gitHubPanel = new();
private MergeConfirmationDialog _mergeDialog = new();
```

### 5.2. Mudanças em OnLoad.cs — Mouse hits extras

```csharp
// Dentro do mouse.MouseDown callback existente, após os checks existentes de status bar:

if (_statusBar.BranchBounds.Contains(pos.X, pos.Y))
{
    // Toggle branch selector dropdown abaixo do botão
    _branchSelector.Show(_statusBar.BranchBounds);
    return;
}

if (_gitHubPanel.Bounds.Contains(pos.X, pos.Y))
{
    _gitHubPanel.HandleClick(pos.X, pos.Y);
    return;
}
```

### 5.3. Mudanças em OnLoad.cs — Teclado extra

Mapear novos shortcuts no handler `keyboard.KeyDown` (dentro do loop existing de key events):

| Combinação | Ação | Handler |
|-----------|------|---------|
| `Ctrl+Shift+G` | Abrir/fechar painel GitHub | `_gitHubPanel.IsVisible = !IsVisible` |
| `Ctrl+Shift+B` | Abrir branch selector | `_branchSelector.Show(...)` |
| `Ctrl+Shift+C` | Focar tab Commits no panel | `_gitHubPanel.ActiveTab = 0; IsVisible=true` |

### 5.4. Mudanças em Render.cs

Na render loop (`OnRender`), após renders existentes de componentes:

```csharp
// GitHub overlays (não-IComponent) — renderizar se visíveis
if (_branchSelector.IsVisible)
    _branchSelector.Render(canvas, _currentTheme);

if (_gitHubPanel.IsVisible)
    _gitHubPanel.Render(canvas, _currentTheme);

if (_mergeDialog.IsVisible)
    _mergeDialog.Render(canvas, new SKRect(0, 0, width, height), _currentTheme);
```

### 5.5. InputHandler — Novos eventos delegate

**Arquivo:** `SLText.Core/Engine/InputHandler.cs` (editar)

Adicionar novos events públicos e mapeamentos nos dicts de shortcuts:

```csharp
// Novos events públicos
public event Action? OnOpenGitHubPanelRequested;
public event Action<string>? OnCheckoutBranchRequested;
public event Action? OnPushCommitsRequested;
public event Action? OnFetchUpdatesRequested;

// No constructor, dentro dos dicts _undoableShortcuts/_immediateShortcuts:
("Ctrl+Shift+G", () => OnOpenGitHubPanelRequested?.Invoke());
("Ctrl+Shift+B", () => OnCheckoutBranchRequested?.Invoke(""));
("Ctrl+Shift+F", () => OnFetchUpdatesRequested?.Invoke());
```

### 5.6. WindowManager subscribe em OnLoad

```csharp
_inputHandler.OnOpenGitHubPanelRequested += () =>
{
    _gitHubPanel.IsVisible = !_gitHubPanel.IsVisible;
};

_inputHandler.OnPushCommitsRequested += async () =>
{
    if (_gitHubService.State.IsAuthenticated && _gitHubService.State.HasActiveRepo)
    {
        await _gitHubService.PushAsync();
        _gitHubPanel.RefreshCurrentTab();
    }
};
```

---

## 6. Sequência de Implementação (Fases)

### Fase 1 — Fundação (Core services + models) ⏱ ~8-12h
- [ ] **1.1** Criar todos os modelos POCO em `SLText.Core.Engine.Model/`
- [ ] **1.2** Adicionar NuGet package `LibGit2Sharp` ao `SLText.Core.csproj`
- [ ] **1.3** Verificar libgit2 native libraries no ambiente dev Linux: `ldconfig -p \| grep libgit2`
- [ ] **1.4** Implementar `GitNativeService` com LibGit2Sharp — abrir repo, listar branches, commits, status
- [ ] **1.5** Registrar GitHub OAuth App nas settings do GitHub (https://github.com/settings/developers)
- [ ] **1.6** Implementar `GitHubAuthService` — Device Flow completo com polling
- [ ] **1.7** Implementar `GitHubTokenStore` — encrypted file storage fallback
- [ ] **1.8** Implementar `GitHubApiService` — endpoints REST básicos (repo info, remote log, PRs)
- [ ] **1.9** Implementar `GitHubIntegrationService` orchestrator

### Fase 2 — Status Bar Integration ⏱ ~4-6h
- [ ] **2.1** Adicionar `_branchBounds` + texto na `StatusBarComponent.Render()`
- [ ] **2.2** Hit-test no `WindowManager.OnLoad` para botão branch
- [ ] **2.3** Exibir branch atual via `_gitHubService.GetCurrentBranch()`
- [ ] **2.4** Atualizar barra após checkout/switch branch
- [ ] **2.5** Ícone visual de estado da conexão (connected/disconnected)

### Fase 3 — Branch Management ⏱ ~6-8h
- [ ] **3.1** Implementar `BranchSelectorOverlay` — lista scrollável, hit-testing interno
- [ ] **3.2** Listar branches locais e remotos via `_gitService.GetBranchesAsync()`
- [ ] **3.3** Checkout ao clicar — chama `_gitService.SwitchBranchAsync(name)`
- [ ] **3.4** Criar novo branch direto pelo selector
- [ ] **3.5** Sync automático quando git reporta mudança de branch externamente

### Fase 4 — Commit Operations ⏱ ~6-8h
- [ ] **4.1** Implementar `GitHubPanelComponent` — estrutura com abas, layout geral
- [ ] **4.2** Tab Commits: renderizar lista paginada de commits
- [ ] **4.3** Exibir SHA curto, mensagem, autor, data relativa
- [ ] **4.4** Expandir detalhes ao clicar (abre `CommitDetailOverlay`)
- [ ] **4.5** Cherry-pick e revert via modal de confirmação
- [ ] **4.6** Refresh automático após commit ou fetch

### Fase 5 — Advanced Panel Features ⏱ ~8-10h
- [ ] **5.1** Tab Pull Requests: listar PRs abertos
- [ ] **5.2** Tab Changes: staged/unstaged files com checkbox visual
- [ ] **5.3** Stage individual file via botão inline
- [ ] **5.4** Visual diff básico em `CommitDetailOverlay` (truncado por linha)
- [ ] **5.5** Notificações banner (rate limit, auth expired, sync errors)

### Fase 6 — Merge & PR Workflow ⏱ ~6-8h
- [ ] **6.1** Implementar `MergeConfirmationDialog`
- [ ] **6.2** Fluxo fast-forward vs merge-commit detection
- [ ] **6.3** Listar conflicts encontrados (paths + diff snippet)
- [ ] **6.4** Dropdown merge method (merge/squash/rebase)
- [ ] **6.5** Criar PR via modal com campos title/body/draft
- [ ] **6.6** Merge PR via API GitHub (REST POST /pulls/{number}/merge)

### Fase 7 — Polimento ⏱ ~4-6h
- [ ] **7.1** Theming completo em todos componentes novos
- [ ] **7.2** Estado sincronizado via events de GitNativeService.StateChanged
- [ ] **7.3** Tratamento erros UX — rate limit banner, offline state, token refresh UI
- [ ] **7.4** Sync de estado periódico (file watcher ou poll a cada 30s)
- [ ] **7.5** Documentação README atualizada com fluxo de setup GitHub Auth
- [ ] **7.6** Testes unitários nos serviços Core (mock HttpClient/LibGit2Sharp)

---

## 7. Dependências Necessárias

### NuGet Packages:
```xml
<!-- SLText.Core.csproj -->
<PackageReference Include="LibGit2Sharp" Version="0.30.0" />
<!-- Opcional: Para keyring integration (Linux/macOS credential store) -->
<PackageReference Include="SecretStorage" Version="3.0.0" 
    Condition="$([MSBuild]::IsOSPlatform('Linux'))" />
```

### GitHub OAuth Registration:
Registrar uma GitHub App ou OAuth App em:
https://github.com/settings/developers

Configurações mínimas:
- **Application type**: Native app
- **Authorization callback URL**: `http://localhost:9876/callback` (fallback se polling falhar)
- **Scopes**: `repo`, `read:org`
- **Permissões**: Repository → Contents: Read and write, Pull requests: Read and write

### Credential Storage (cross-platform):
| Platform | Mechanism | Priority |
|----------|-----------|----------|
| Windows | Windows Credential Manager (via SecretStorage) | First choice |
| macOS | macOS Keychain (via SecretStorage) | First choice |
| Linux | GNOME Secret Service / KDE Wallet (via SecretStorage) | First choice |
| Fallback | Arquivo criptografado AES-256 em `~/.config/sltext/github-tokens.enc` | Always available |

Chave de descriptografia derivada de máquina única usando .NET `CryptographicProvider` + user profile hash.

---

## 8. Considerações Técnicas

### Performance:
- Operações Git devem ser sempre **assíncronas** (Task.Run / async-await) seguindo padrão LSP Service
- Commits list deve usar **paginação lazy** (carregar 50, scroll carrega mais)
- Polling notificações: max **1x/min** para evitar rate limit

### Rate Limits (GitHub API v3 REST):
- **Authenticated (token)**: 5,000 requests/hour
- **Unauthenticated**: 60 requests/hour
- Se atingir rate limit: mostrar banner vermelho "Rate limit exceeded. Try again in X minutes"
- Respeitar headers `X-RateLimit-Remaining` e `X-RateLimit-Reset` em todas respostas HTTP

### Segurança:
- Tokens NUNCA aparecem em logs/console
- Tokens encriptados em storage (AES-256 como fallback)
- Limpar token na logout/clear cache via `GitHubTokenStore.Clear()`
- Não enviar tokens em error stack traces (strip de sensitive data)
- User code no Device Flow: exibir em monospace maiúsculo para facilitar cópia

### Thread safety:
- Todos serviços usam `_gate = new SemaphoreSlim(1, 1)` exatamente como `LspService`
- Callbacks de UI disparados via SynchronizationContext ou verificação de thread
- Dispor corretamente recursos (`IDisposable`, `_gate.Dispose()`)

### Testabilidade:
- Interfaces em `SLText.Core.Interfaces.IGitRepositoryClient` para mockability
- `GitNativeService` pode ter implementação fake `FakeGitService` para testes unitários
- `GitHubApiService` injeta `HttpClient` para poder substituir por `HttpMessageHandler` fake

---

## 9. Diagrama de Componentes (Resumo)

```
WindowManager (central orchestrator)
│
├── StatusBarComponent : IComponent
│   ├── PlayButtonBounds (existing)
│   ├── SelectorBounds (existing)
│   └── BranchBounds (NEW)
│       └── Click → Toggle BranchSelectorOverlay
│
├── ModalComponent (existing)
│   └── Used for simple confirmations only
│
├── BranchSelectorOverlay (NEW — non-IComponent)
│   ├── bool IsVisible
│   ├── Render(SKCanvas canvas, EditorTheme theme)
│   ├── HandleClick(float x, float y)
│   └── DrawSelectableList(branch items)
│
├── GitHubPanelComponent (NEW — non-IComponent overlay)
│   ├── bool IsVisible
│   ├── ActiveTab: int
│   ├── Bounds: SKRect
│   ├── Sub-tabs: Commits | PRs | Changes | Branches
│   └── Each tab has its own item list renderer
│
├── CommitDetailOverlay (NEW — non-IComponent popup)
│   ├── Shows when clicking a commit in panel
│   ├── Diff viewer between commits
│   └── Scroll X + Y support
│
├── MergeConfirmationDialog (NEW — non-IComponent)
│   ├── Conflict display area
│   ├── Merge method dropdown
│   └── 3-button layout (Confirm / Cancel / View Conflicts)
│
└── CommandPaletteComponent (existing — can be extended)
    └── Can host GitHub commands like "Create PR", "Switch Branch"

Core Services (SLText.Core.Engine.Git/)
├── GitHubIntegrationService (orchestrator — main entry point)
│   ├── GitHubAuthService → OAuth 2.0 Device Flow
│   │   └── Emits DeviceFlowCallback(string userCode, Uri verificationUri)
│   ├── GitHubApiService → REST calls (HttpClient)
│   │   └── Protected by _gate semaphore
│   ├── GitNativeService → LibGit2Sharp wrapper
│   │   └── Protected by _gate semaphore
│   └── GitHubTokenStore → Encrypted persistence
│
Data Models (SLText.Core.Engine.Model/)
├── GitBranch, CommitInfo, ChangedFile, PullRequestInfo
├── MergeResult, RemoteBranch, RepositoryInfo
├── CreatePRRequest, IssueInfo, NotificationInfo
└── GitIntegrationState
```

---

## 10. Checklist de Pré-Requisitos

Antes de começar a implementaçao:

- [ ] Criar/register GitHub OAuth App em https://github.com/settings/developers
- [ ] Anotar `CLIENT_ID` e `CLIENT_SECRET` (se necessário para refresh)
- [ ] Instalar `LibGit2Sharp` NuGet package no `SLText.Core.csproj`
- [ ] Verificar libgit2 native libraries no ambiente Linux: `ldconfig -p | grep libgit2`
- [ ] Definir política de storage de credenciais (keyring via SecretStorage vs encrypted file)
- [ ] Escolher porta para callback HTTP fallback (`localhost:9876` recomendado)
- [ ] Configurar scopes mínimos: `repo`, `read:org`

---

## 11. Arquitetura de Dados Fluindo

```
[User clicks "main" in status bar branch button]
    ↓
WindowManager.OnLoad mouse handler detects click on _statusBar.BranchBounds
    ↓
BranchSelectorOverlay.Show() appears with local + remote branches
    ↓
User clicks "dev" → WindowManager calls _gitHubService.SwitchBranchAsync("dev")
    ↓
GitHubIntegrationService → GitNativeService.SwitchBranchAsync("dev")
    ↓
LibGit2Sharp Repository.Head.Fetch() switches ref
    ↓
State notification flows back: _gitHubService.OnStateChanged → branch name updated
    ↓
StatusBarComponent gets new branch name → re-renders with "▲ dev ▾"
    ↓
Next frame: OnRender redraws status bar with updated text
```

---

## Notas Finais

### Escopo mínimo viável (MVP):
1. Conexão + branch display na status bar
2. Troca de branch via selector overlay
3. Fetch/push básico
4. Visualização de commits recentes no panel

### Feature parciais descartáveis (prioridade baixa):
- Cherry-pick via UI
- Inline diff viewer de conflicts
- GraphQL queries complexas
- Issue management

### Sugestão de ordem de trabalho:
Começar pela **Fase 1 → Fase 2** para ter algo funcional rapidamente (ver branch na status bar, trocar branch). Depois evoluir para commits e PRs incrementalmente. Cada fase autônoma entregando um slice visível.

### Riscos técnicos identificados:
- **Wayland + OpenGL**: Se SkiaSharp não criar GRContext no Wayland, nenhuma UI funciona (já documentado no código existente)
- **Rate limit GitHub API**: 5k req/h é generoso mas polls frequentes podem atingir — usar ETag caching e verificar X-RateLimit-Remaining
- **Credential storage no Linux headless**: Sem session D-Bus, SecretStorage falha — fallback encrypted file obrigatório
- **LibGit2Sharp native deps**: Em distribuições minimalistas, libssl/libcurl ausentes — package manager must-have
