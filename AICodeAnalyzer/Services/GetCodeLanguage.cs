namespace AICodeAnalyzer.Services;

public static class GetCodeLanguage
{
    public static string GetLanguageForExtension(string ext)
    {
        return ext switch
        {
            // C# and .NET
            ".cs" => "csharp",
            ".vb" => "vb",
            ".fs" => "fsharp",
            ".xaml" => "xml",
            ".csproj" => "xml",
            ".vbproj" => "xml",
            ".fsproj" => "xml",
            ".nuspec" => "xml",
            ".aspx" => "aspx",
            ".asp" => "asp",
            ".cshtml" => "cshtml",
            ".axaml" => "xml",

            // Web languages
            ".html" => "html",
            ".htm" => "html",
            ".css" => "css",
            ".js" => "javascript",
            ".jsx" => "jsx",
            ".ts" => "typescript",
            ".tsx" => "tsx",
            ".vue" => "vue",
            ".svelte" => "svelte",
            ".scss" => "scss",
            ".sass" => "sass",
            ".less" => "less",
            ".mjs" => "javascript",
            ".cjs" => "javascript",

            // JVM languages
            ".java" => "java",
            ".kt" => "kotlin",
            ".scala" => "scala",
            ".groovy" => "groovy",

            // Python
            ".py" => "python",

            // Ruby
            ".rb" => "ruby",
            ".erb" => "erb",

            // PHP
            ".php" => "php",

            // C/C++
            ".c" => "c",
            ".cpp" => "cpp",
            ".h" => "cpp", // C/C++ headers typically get cpp highlighting

            // Go
            ".go" => "go",

            // Rust
            ".rs" => "rust",

            // Swift/Objective-C
            ".swift" => "swift",
            ".m" => "objectivec",
            ".mm" => "objectivec",

            // Dart/Flutter
            ".dart" => "dart",

            // Markup and Data
            ".xml" => "xml",
            ".json" => "json",
            ".yaml" => "yaml",
            ".yml" => "yaml",
            ".md" => "markdown",
            ".txt" => "text",
            ".plist" => "xml",

            // Templates
            ".pug" => "pug",
            ".jade" => "jade",
            ".ejs" => "ejs",
            ".haml" => "haml",

            // Query Languages
            ".sql" => "sql",
            ".graphql" => "graphql",
            ".gql" => "graphql",

            // Shell/Scripts
            ".sh" => "bash",
            ".bash" => "bash",
            ".bat" => "batch",
            ".ps1" => "powershell",
            ".pl" => "perl",

            // Other Languages
            ".r" => "r",
            ".lua" => "lua",
            ".dockerfile" => "dockerfile",
            ".ex" => "elixir",
            ".exs" => "elixir",
            ".jl" => "julia",
            ".nim" => "nim",
            ".hs" => "haskell",
            ".clj" => "clojure",
            ".elm" => "elm",
            ".erl" => "erlang",
            ".asm" => "asm",
            ".s" => "asm",
            ".wasm" => "wasm",

            // Configuration/Infrastructure
            ".ini" => "ini",
            ".toml" => "toml",
            ".tf" => "hcl",
            ".tfvars" => "hcl",
            ".proto" => "proto",
            ".config" => "xml",

            // Default case
            _ => "text"
        };
    }
}