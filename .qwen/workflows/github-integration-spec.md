# Especificação: Integração GitHub no SLText — Fases Restantes

> **Última atualização:** 2026-09-14
> **Status atual:** ✅ Fase 2 completa — build passing, branch button funcional na status bar
> **Arquivo anterior completo:** ver git log para histórico de mudanças

## O que JÁ ESTÁ IMPLEMENTADO

### Fase 2 — Status Bar Integration (COMPLETA) ✅

| Categoria | Status | Detalhes |
|-----------|--------|----------|
| Branch button na StatusBarComponent | ✅ | `BranchButtonBounds`, `_currentBranch`, `SetBranchName()`, `SetGitConnection()` |
| Visual indicator de conexão | ✅ | Condição `_hasGitConnection` muda cor do texto e play button |
| Event wiring no WindowManager | ✅ | `ConnectionStateChanged` atualiza `_statusBar` via `InvokeOnUi()` |
| Hit-test do botão de branch | ✅ | OnLoad.cs detecta clique → chama `ToggleBranchSelector()` |
| ToggleBranchSelector (stub) | ✅ | Mostra modal com branch atual até Fase 3 ser implementada |
| ConnectToRepository | ✅ | Método limpo, verifica `.git` dir, conecta serviço |
| Auto-connect ao abrir folder | ✅ | Se diretório tem repo, conecta automaticamente |
| Auto-connect no startup | ✅ | Se `LastRootDirectory` salvo nos settings, conecta sem abrir pasta manualmente |
| RefreshExplorerWithGit | ✅ | Refresh explora quando estado do repo muda |
| InvokeOnUi helper | ✅ | Padrão `_pendingAction` para cross-thread marshalling |
| Layout correto na Status Bar | ✅ | Elementos desenhados da direita p/ esquerda sem sobreposição |

**Arquivos modificados na Fase 2:**
```
SLText.View/Components/StatusBarComponent.cs    — branch bounds + layout correto
SLText.View/UI/WindowManager.cs                 — service, events, connect, stubs
SLText.View/UI/OnLoad.cs                        — branch button hit-test
SLText.Core/Engine/Git/GitHubIntegrationService.cs — explicit state update after Open()
SLText.Core/Engine/Git/GitNativeService.cs      — fire StateChanged after Open()
```

### Fase 1 — Core Services (COMPLETA) ✅

| Categoria | Status | Detalhes |
|-----------|--------|----------|
| Modelos POCO | ✅ 11 arquivos | Git models + DTOs para API responses |
| `IGitRepositoryClient` interface | ✅ | Contrato em `SLText.Core/Interfaces/` |
| `GitHubAuthService` | ✅ | OAuth 2.0 Device Flow com polling |
| `GitHubTokenStore` | ✅ | AES-256 encryption + file persistence |
| `GitHubToken` | ✅ | DTO com `DateTimeOffset` expiry |
| `GitNativeService` | ✅ | Wrapper LibGit2Sharp 0.30 completo (17 métodos) |
| `GitHubApiService` | ✅ | HTTP client REST v3 (repos, commits, PRs, issues, notifications) |
| `GitHubJsonContext` | ✅ | DTOs JSON para API responses |
| `GitHubIntegrationService` | ✅ | Orquestrador unificado (auth + api + git-local) |
| Dependências NuGet | ✅ | `LibGit2Sharp 0.30.0` no `.csproj` |
| Testes | ✅ 88 testes passando | Token store, models, tests — todos passando |

---

## Implementação Detalhada da Fase 2

### 2.1. Layout da Status Bar

Elementos desenhados da **direita para a esquerda**:
```
┌─────────────────────────────────────────────────────┐
│ 23 L | C# | 14pt        [Arquivo.cs]    ▶ Config... │
│              ▲ main ▾          Ln 1, Col 1           │
└─────────────────────────────────────────────────────┘
         Branch    Play     Selector    Cursor pos
```

Cálculo: cursor → gap → selector → gap → play → gap → branch. Todos os bounds calculados com `_font.MeasureText()`.

### 2.2. Conexão Automática

**Via menu "Abrir Pasta":**
1. `OpenFolderRequested` event → `Task.Run(async)`
2. Carrega project files via LSP (`_lspService.LoadProjectFiles`)
3. Chama `await ConnectToRepository(folder)`
4. Verifica se `.git` existe → chama `_gitHubService.ConnectToRepositoryAsync(path)`
5. `ConnectionStateChanged` event dispara → `InvokeOnUi()` atualiza `_statusBar`

**No startup (quando há projeto salvo nos settings):**
Quando `_settings.LastRootDirectory` é válido:
```csharp
if (!string.IsNullOrEmpty(_settings.LastRootDirectory) && Directory.Exists(...))
{
    _lastDirectory = _settings.LastRootDirectory;
    SetCurrentFile(_settings.LastRootDirectory);
    Task.Run(async () => await ConnectToRepository(_lastDirectory));
}
```
Resultado: ao iniciar o app com um projeto já salvo, a branch aparece automaticamente na status bar sem precisar abrir pasta manualmente.

### 2.3. Correção Crítica na Conexão

Bug identificado e corrigido: `GitNativeService.Open()` não disparava `StateChanged` automaticamente.

**Fix em `GitNativeService.Open()`:**
```csharp
_repo = new Repository(path);
RefreshState();
// Fire state change to notify listeners
StateChanged?.Invoke(this, new GitStateChangedEventArgs());
```

**Fix em `GitHubIntegrationService.ConnectToRepositoryAsync()`:**
```csharp
_git.Open(path);
if (_git.CurrentBranch != null) {
    State.CurrentBranch = _git.CurrentBranch; // Explicit read
}
State.HasActiveRepo = true;
ConnectionStateChanged?.Invoke(this, State);
```

Sem essa correção, o `CurrentBranch` ficava vazio mesmo conectado ao repositório.

### 2.4. Stub para Fase 3

`ToggleBranchSelector()` abre um modal informativo:
```csharp
private void ToggleBranchSelector()
{
    string? branch = _gitHubService.State.CurrentBranch ?? "(no branch)";
    _modal.Show("Branch Selector", $"Current branch: {branch}\n\n(Fase 3 — implementar overlay)", null, null, null);
}
```

Quando Fase 3 for implementada, substituir por `BranchSelectorOverlay.Show(...)`.

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

Começar pela **Fase 3** — Branch Selector Overlay:
1. Criar `BranchSelectorOverlay.cs` seguindo padrão `AutocompleteComponent` / `ModalComponent`
2. Lista scrollável com branches locais e remotos
3. Clique seleciona → chama `_gitHubService.SwitchBranchAsync(name)`
4. Campo de texto para criar novo branch
5. Substituir `ToggleBranchSelector()` pelo overlay real
