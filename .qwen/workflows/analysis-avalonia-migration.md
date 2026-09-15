# Migração de SLText.View: Silk.NET/SkiaSharp → Avalonia

## Contexto

SLText tem duas camadas bem separadas:

| Camada | Pacote | Responsabilidade |
|--------|--------|-----------------|
| **SLText.Core** | LibGit2Sharp, Roslyn, TextCopy | Modelo de documento, comandos de edição, LSP, GitHub, input handling — zero dependência UI |
| **SLText.View** | Silk.NET (Windowing/Input/OpenGL), SkiaSharp | Janela GLFW, loop de renderização, layout manual com `SKRect`, desenho pixel-a-pixel via `SKCanvas` |

O problema atual: a View implementa manualmente toda a geometria — cálculo de posição do mouse → linha/columna, scroll X/Y, cursor-scroll-into-view, dimensão dos componentes por frame (`Update(double)`), layout dos children no `OnRender()`. Essa camada é a fonte principal de bugs de posicionamento e manutenção custosa.

A Core já é 100% agnóstica a UI. Não existem referências a Avalonia, WPF, GTK#, ou qualquer toolkit em todo o projeto.

---

## Análise de viabilidade

### ✅ O que pode ser reutilizado da Core (100%)

Todo o código de `SLText.Core` pode ser mantido sem alterações:

- **TextBuffer** — modelo de documento
- **CursorManager** — tracking de linha/columna/seleção
- **UndoManager** — history stack
- **InputHandler** — mapeamento de shortcuts, buffers de digitação, paste/cut/copy
- **Todos os Commands** — BackspaceCommand, TypingCommand, EnterCommand, etc.
- **LspService** — Roslyn diagnostics, completion, signature help
- **GitHubIntegrationService** — OAuth + API + operações git locais
- **TabExpansion** — conversão tab ↔ espaço
- **SyntaxProvider / GrammarDefinitions** — regex grammars por linguagem
- **IndentationProvider** — estratégias de indentação

Nenhuma classe da Core referencia tipos de UI (`SKCanvas`, `SKRect`, viewport dimensions, pixels). A Core opera puramente com buffer indices + cursor line/column.

### ❌ O que seria eliminado ao migrar para Avalonia

Na View atual, os seguintes trechos seriam removidos completamente:

| Arquivo (View) | O que faz | Tratamento em Avalonia |
|---------------|-----------|----------------------|
| `Components/Canvas/EditorComponent.cs` (~700 linhas) | Renderiza background, gutter, texto syntax-highlighted, cursor blink, selection rects, diagnostic squiggles, search highlights, bracket crosshairs, indent guides, scrollbars | Substituído por controles nativos (`RichTextBox` customizado ou `TextBoxEx` do [`Avalonia.Controls.Pack`](https://github.com/wieslawsoltes/AvaloniaControls)); text rendering fica por conta do toolkit |
| `Components/Canvas/TextRenderer.cs` | Tokeniza texto por regex, desenha cada span colorido via `SKCanvas.DrawText()` | Syntax highlighting integrado via `TextPattern` ou extensão do `TextView` do Avalonia |
| `Components/Canvas/GutterRenderer.cs` | Desenha números de linha, ícones de diagnóstico | Linha numbers num painel lateral nativo (`ItemsControl`) |
| `Components/Canvas/BracketRenderer.cs` | Crosshair highlight entre chaves pareadas | Feature visual implementada como overlay control |
| `Components/Canvas/IndentGuideRenderer.cs` | Linhas verticais de indentação | Implementado como `Border` thin com cor de tema |
| `Components/Canvas/ViewportManager.cs` | Scroll X/Y, clamping, cursor-scroll-into-view, mouse→document coordinate mapping | Scroll automático pelo `ScrollViewer` nativo do Avalonia |
| `UI/Render.cs` | Setup de coordinate system, chamadas a `SKCanvas` por frame | Eliminado — Avalonia gerencia o render loop |
| `UI/Update.cs` | Per-frame update pass para todos os components | Eliminado — Avalonia usa data binding + event-driven updates |
| `Abstractions/IComponent.cs` | Interface `Bounds`, `Render(SKCanvas)`, `Update(double)` | Eliminado — padrão de componente customizado não se aplica mais |

### ⚠️ O que precisaria adaptação

| Componente (View) | Adapt Needed |
|-------------------|-------------|
| `AutocompleteComponent.cs` | Reimplementar como `AutoCompleteBox` customizado (há pacotes NuGet ou implementar com `ComboBox` + `ListView`) |
| `BranchSelectorOverlay.cs` | Reimplementar como popup selector nativo |
| `CommandPaletteComponent.cs` | Reimplementar com `ListBox`/`ComboBox` em overlay; já existe padrão consolidado no Avalonia |
| `ContextMenuComponent.cs` | Usar `ContextMenu` nativo do Avalonia |
| `FileExplorerComponent.cs` | `TreeView` nativo com ViewModel bindado à estrutura de arquivos |
| `ModalComponent.cs` | `Window` modal do Avalonia |
| `SearchComponent.cs` | Custom control com `TextBox` + `Button` + results panel |
| `SignatureHelpComponent.cs` | Overlay com `ListBox` mostrando parâmetros da assinatura |
| `StatusBarComponent.cs` | Barra inferior simples com bindings |
| `TabComponent.cs` | `TabControl` customizado (drag-to-reorder, close buttons) — há `Panels'Uml` ou `CommunityToolkit.Avalonia.Controls` |
| `TerminalComponent.cs` | Integrar com `Tmds.ConEmuBridge` ou similar, ou usar terminal embutido via `LibVTE-sharp` |
| `Services/EditorSettings.cs` | Settings persistence — já é POCO, só mudar storage location para Avalonia config |
| `Services/NativeDialogService.cs` | Trocar `NativeFileDialogSharp` por `OpenFilePicker`/`SaveFilePicker` do Avalonia |
| `Services/RunService.cs` | Provavelmente sem mudanças (executa processos, lê output) |
| `Styles/EditorTheme.cs` | Converter paletes de cores para `Color` + `Style` resources do Avalonia |
| `Styles/SyntaxProvider.cs` e `Languages/*.cs` | Grammars regex permanecem; integrar com tokenization do editor Avalonia |
| `UI/IntelliSense.cs` | Posicionamento absoluto → posicionamento relativo/anchor do Avalonia |
| `UI/KeyCDown.cs`, `UI/LifeCycles.cs`, `UI/OnLoad.cs` | Refatorar para lifecycle do `Window`/`UserControl` do Avalonia |
| `UI/WindowManager.cs` | Eliminar GLFW setup → criar `Application` entry point Avalonia |
| `UI/Input/InputManager.cs` | Dispatcher centralizado pode ser simplificado com `InputBindings` + ` ICommand` bindings do Avalonia |
| `UI/Input/KeyboardMapper.cs` | Mapeamento `Silk.NET.Key` → pode ser descartado; Avalonia tem seu próprio enum |
| `UI/Input/MouseHandler.cs` | Click/drag/scroll handlers viram eventos `Pointer*` do Avalonia |

---

## Estimativa de esforço

### Fases sugeridas

| Fase | Escopo | Estimativa |
|------|--------|------------|
| **Phase 0 — Infraestrutura** | Criar solução com SLText.Core (sem mudanças) + SLText.View.Avalonia; configurar `Application` entry point, window básica, tema base | 1–2 dias |
| **Phase 1 — Document Editor Core** | Integrar `TextBuffer` + `CursorManager` + `UndoManager` + `InputHandler` com editor de texto Avalonia; exibir texto, cursor, undo/redo funcionando | 3–5 dias |
| **Phase 2 — Syntax Highlighting** | Integrar `SyntaxProvider` + grammar definitions com a tokenização do editor Avalonia; colors de tema aplicadas | 2–3 dias |
| **Phase 3 — Tab System** | Tabs com `TabControl` customizado; open/save/rename/close tabs | 2–3 dias |
| **Phase 4 — Gutter + Diagnostics** | Line numbers, gutter icons, diagnostic squiggles (Roslyn diagnostics integrados via `LspService`) | 2–3 dias |
| **Phase 5 — Overlays & Panels** | Search, autocomplete, context menu, file explorer, branch selector, command palette, signature help, status bar | 5–8 dias |
| **Phase 6 — Terminal + Run** | Terminal embutido, run configuration service | 2–3 dias |
| **Phase 7 — Polish & Migration** | Themes completos, keyboard shortcuts mapeados, drag-and-drop, file explorer, git operations na UI | 3–5 dias |

**Total estimado: ~20–30 dias de desenvolvimento** (dependendo da familiaridade com Avalonia).

### Comparação de responsabilidades

| Aspecto | Silk.NET/SkiaSharp (atual) | Avalonia (proposto) |
|---------|--------------------------|-------------------|
| Window creation | GLFW manual (`Window.Create`) | `ApplicationStartup` handler |
| Render loop | `OnRender()` a ~60fps, desenha tudo manualmente | Data binding + compositor nativo (GPU-accelerated mas declarativo) |
| Layout | `SKRect` calculado manualmente no `Update()` | `Grid`/`StackPanel`/`DockPanel` declarativo em XAML |
| Input handling | `InputManager` custom com LIFO/topmost-first mouse | `InputBindings`, routed events, `Pointer*` events |
| Scrolling | `ViewportManager` com clamping manual | `ScrollViewer` nativo |
| Mouse → position | `CharWidth * col = x` approximation | Toolkit dá caret index via `TextInput` ou `GetPositionFromPoint` |
| Font rendering | SkiaSharp `SKFont` direct to OpenGL | Native font rasterizer (FreeType no Linux, DirectWrite no Windows) |
| Theming | C# enums + `SKColor` passed to renderers | `DynamicResource` + `ColorScheme` no XAML |
| Cross-platform | GLFW + SkiaSharp (works, but you maintain everything) | Avalonia guarantees一致的 behavior across Linux/Windows/macOS |

---

## Riscos e considerações

### Vantagens da migração
- **Zero código de layout/render manual** — o problema raiz da tarefa atual desaparece
- **Menos código boilerplate** — XAML + bindings substituem centenas de linhas de desenhar rects e calcular coordenadas
- **Consistência cross-platform** — Avalonia lida com diferenças de DPI, fontes, e rendering APIs entre Linux (X11/Wayland) e Windows
- **Comunidade ativa** — pacotes NuGet abundantes (`Avalonia.Diagnostics`, `CommunityToolkit.Avalonia`, `Avalonia.Controls.ColorPicker`, etc.)

### Riscos
- **Performance de renderização em documentos grandes** — Avalonia usa virtualização; documentos com milhões de caracteres podem precisar de otimizações customizadas (mas Silk.NET também sofre nesse cenário)
- **Terminal embutido** — atualmente usa algum backend custom; migração pode exigir biblioteca externa (`Tmds.ConEmuBridge` ou similar)
- **Learning curve** — se não há experiência prévia com Avalonia/XAML, as primeiras fases serão lentas
- **Breaking changes em futuras versões do .NET** — Avalonia geralmente acompanha LTS releases; `.NET 10` (framework atual) provavelmente já é suportado

### Quando NÃO migrar

Se a prioridade é estabilidade imediata e o custo de debugging de position/layout vale menos que a dor, considere alternativas de menor escopo:

1. **Isolar ViewportManager** — extrair toda a geometria de positioning para uma classe pura testável (unit tests com inputs conhecidos)
2. **Adotar VirtualizingPanel** — ainda dentro de SkiaSharp, virtualizar linhas visíveis ao invés de desenhar todas
3. **Usar Avalonia apenas para painéis externos** — manter editor custom em SkiaSharp, mas mover tab bar, status bar, file explorer para Avalonia

---

## Veredito

**Sim, é viável e recomendável migrar para Avalonia.**

- A Core já é 100% reutilizável — nenhuma linha precisa mudar
- Todo o código de layout/manual render (~700+ linhas de `EditorComponent` + renderers + `ViewportManager`) seria eliminado
- Os componentes restantes são padrões de UI bem documentados no ecossistema Avalonia
- O retorno sobre investimento aparece na Phase 1+: menos bugs de posicionamento, menos código para manter, temas configuráveis em XAML

**Recomendação:** começar com uma branch separada (`feature/avalonia-migration`), migrar incrementalmente fase por fase conforme listado acima, e manter ambas as Views coexistindo durante a transição via feature flag.
