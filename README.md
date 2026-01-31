SLText

SLText é um editor de texto moderno e de alto desempenho desenvolvido em C# 
utilizando a biblioteca SkiaSharp para renderização acelerada por GPU. 
O projeto foca em oferecer uma experiência de digitação fluida e 
uma interface minimalista para desenvolvedores.

🛠️ Instalação
Linux (Debian/Ubuntu)

O pacote .deb agora gerencia todas as dependências automaticamente.

    Baixe a versão mais recente em Releases.

    Instale usando o comando:
    Bash

    sudo apt install ./SLText_0.x_Linux_x64.deb

Windows

    Baixe o instalador .exe em Releases.

    Execute o assistente de instalação (Inno Setup).

    O editor será adicionado ao seu Menu Iniciar e ao Menu de Contexto (Botão direito: "Open with SLText").

🛠️ Tecnologias Utilizadas

    C# / .NET 10

    SkiaSharp: Para renderização gráfica de alta performance.

    Silk.NET: Para gerenciamento de janelas e entrada via GLFW.

    TextCopy: Para integração com a área de transferência do sistema.

💻 Funcionalidades de Edição

O SLText não é apenas um visualizador de texto; ele inclui inteligência para auxiliar na escrita de código:

    Syntax Highlighting: Realce de sintaxe baseado em definições de linguagem integradas para melhor legibilidade.

    Auto-Pairing (Auto-Fechamento): Inserção automática de caracteres de fechamento para manter a integridade do código. Os pares suportados incluem:

        Parênteses () e Colchetes [].

        Chaves {}.

        Aspas simples '' e duplas "".

    Gutter de Linhas: Barra lateral com numeração de linhas para fácil navegação pelo arquivo.


📚 Linguagens Suportadas

Graças ao provedor de sintaxe (SyntaxProvider), o editor reconhece e processa as seguintes linguagens nativamente:
Linguagem	Descrição
C#	Suporte completo para desenvolvimento .NET.
HTML / Razor	Estruturação web e componentes Blazor/ASP.NET.
CSS	Estilização de interfaces.
JavaScript	Lógica de programação para web.
XML	Arquivos de configuração e metadados.
G-Code	Instruções para máquinas CNC e impressão 3D.

⚡ Produtividade com Snippets

O SLText conta com um sistema de Snippets inteligentes que permitem expandir abreviações em blocos de código complexos, economizando tempo de digitação:

    C#: Atalhos rápidos como cw para expandir Console.WriteLine().

    HTML5: Estruturação completa com um clique, incluindo html5 para o boilerplate inicial, 
    
    além de div, ul, img, e links de scripts/estilos.

    CSS Moderno: Snippets para flexbox e media queries, facilitando o design responsivo.

    Cursor Inteligente: O caractere | nos snippets define a posição automática onde o cursor será posicionado 
    
    após a expansão, permitindo continuar a escrita sem interrupções.

⌨️ Atalhos de Teclado - SLText

📄 Gestão de Arquivos

Atalho	Ação

Ctrl + N	Criar um Novo Arquivo (limpa o buffer e reseta o desfazer).

Ctrl + O	Abrir um arquivo existente do disco.

Ctrl + S	Salvar as alterações no arquivo atual.

✏️ Edição e Seleção

Atalho	Ação

Ctrl + C	Copiar o texto selecionado para a área de transferência.

Ctrl + V	Colar o texto da área de transferência na posição do cursor.

Ctrl + X	Recortar o texto selecionado.

Ctrl + A	Selecionar Tudo o que há no documento.

Ctrl + D	Duplicar a linha atual (ou a seleção).

Ctrl + Z	Desfazer a última ação.

Ctrl + Y	Refazer a última ação desfeita.

Shift + Setas	Selecionar texto caractere por caractere ou linha por linha.

Ctrl + L	Selecionar Linha atual por completo.

Ctrl + Shift + K	Deletar Linha atual completamente.

🚀 Movimentação e Navegação

Atalho	Ação

Ctrl + ← / →	Pular uma palavra inteira para a esquerda ou direita.

Ctrl + ↑ / ↓	Mover a visualização em bloco (4 linhas por vez).

Ctrl + Shift + ↑ / ↓	Mover a Linha atual para cima ou para baixo (troca de posição).

🔍 Visualização (Zoom)

Atalho	Ação

Ctrl + Mouse Scroll	Aumentar ou diminuir o Zoom (tamanho da fonte).

Ctrl + 0 (Zero)	Resetar Zoom para o padrão (16pt).

💡 Dicas de Uso

    Seleção de Bloco: Você pode combinar Ctrl + Shift + Setas Laterais para selecionar palavras inteiras rapidamente.

    Auto-Parênteses: Ao digitar (, [, {, " ou ', o editor insere automaticamente o par de fechamento para você.

    Barra de Status: Acompanhe o tamanho atual da fonte (zoom) e a posição exata do cursor no canto inferior direito.



🤝 Como Contribuir

Contribuições são muito bem-vindas! Se queres ajudar a melhorar o SLText:

    Faz um Fork do projeto.

    Cria uma Branch para a tua funcionalidade (git checkout -b feature/NovaFuncionalidade).

    Faz Commit das tuas alterações (git commit -m 'Adiciona nova funcionalidade').

    Faz Push para a Branch (git push origin feature/NovaFuncionalidade).

    Abre um Pull Request.

⚖️ Licença

Este projeto está licenciado sob a GNU General Public License v3.0 (GPL-3.0).
Notas Adicionais para o Linux

Embora o pacote .deb já inclua as dependências necessárias, caso você esteja compilando o código-fonte 
manualmente, ainda poderá precisar das bibliotecas de desenvolvimento do sistema:

Bash

sudo apt update
sudo apt install libglfw3-dev libgles2 libx11-dev libxcursor-dev libxi-dev libxinerama-dev libxrandr-dev xclip xsel
