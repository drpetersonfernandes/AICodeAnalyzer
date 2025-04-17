using System;
using Markdig;

namespace AICodeAnalyzer.Services;

public class HtmlService(LoggingService loggingService)
{
  private readonly LoggingService _loggingService = loggingService;

  private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
    .UseAdvancedExtensions()
    .Build();

  private const string HtmlTemplate = """
                                      <!DOCTYPE html>
                                      <html lang="en">
                                      <head>
                                      <meta charset="utf-8" />
                                      <title>AI Analysis Response</title>
                                      <link href="https://cdn.jsdelivr.net/npm/prismjs@1.28.0/themes/prism-tomorrow.css" rel="stylesheet" />
                                      <style>
                                      body {
                                        font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                                        margin: 1em;
                                      }
                                      pre {
                                        position: relative;
                                        margin-top: 1em;
                                      }
                                      .copy-btn {
                                        position: absolute;
                                        top: 8px;
                                        right: 8px;
                                        background-color: #444;
                                        color: white;
                                        border: none;
                                        padding: 0.3em 0.5em;
                                        cursor: pointer;
                                        border-radius: 3px;
                                        opacity: 0.7;
                                        transition: opacity 0.3s ease;
                                        font-size: 0.8rem;
                                      }
                                      .copy-btn:hover {
                                        opacity: 1;
                                      }
                                      </style>
                                      </head>
                                      <body>
                                      {{Content}}
                                      <script src="https://cdn.jsdelivr.net/npm/prismjs@1.28.0/prism.js"></script>
                                      <script src="https://cdn.jsdelivr.net/npm/prismjs@1.28.0/components/prism-csharp.min.js"></script>
                                      <script src="https://cdn.jsdelivr.net/npm/prismjs@1.28.0/components/prism-python.min.js"></script>
                                      <!-- Add other Prism languages here as needed -->
                                      <script>
                                      document.addEventListener('DOMContentLoaded', () => {
                                        document.querySelectorAll('pre').forEach(pre => {
                                          const copyBtn = document.createElement('button');
                                          copyBtn.textContent = 'Copy';
                                          copyBtn.className = 'copy-btn';
                                      
                                          copyBtn.addEventListener('click', () => {
                                        const code = pre.querySelector('code');
                                        if (!code) return;
                                        const text = code.innerText;
                                      
                                        if (navigator.clipboard) {
                                          navigator.clipboard.writeText(text).then(() => {
                                            copyBtn.textContent = 'Copied!';
                                            setTimeout(() => copyBtn.textContent = 'Copy', 2000);
                                          }).catch(() => {
                                            fallbackCopyText(text, copyBtn);
                                          });
                                        } else {
                                          fallbackCopyText(text, copyBtn);
                                        }
                                      });

                                      function fallbackCopyText(text, button) {
                                        const textArea = document.createElement('textarea');
                                        textArea.value = text;
                                        // Avoid scrolling to bottom
                                        textArea.style.position = 'fixed';
                                        textArea.style.top = '0';
                                        textArea.style.left = '0';
                                        textArea.style.width = '1px';
                                        textArea.style.height = '1px';
                                        textArea.style.padding = '0';
                                        textArea.style.border = 'none';
                                        textArea.style.outline = 'none';
                                        textArea.style.boxShadow = 'none';
                                        textArea.style.background = 'transparent';
                                        document.body.appendChild(textArea);
                                        textArea.focus();
                                        textArea.select();
                                      
                                        try {
                                          const successful = document.execCommand('copy');
                                          if (successful) {
                                            button.textContent = 'Copied!';
                                            setTimeout(() => button.textContent = 'Copy', 2000);
                                          } else {
                                            alert('Failed to copy code.');
                                          }
                                        } catch (err) {
                                          alert('Failed to copy code.');
                                        }
                                      
                                        document.body.removeChild(textArea);
                                      }
                                      
                                          pre.appendChild(copyBtn);
                                        });
                                      });
                                      </script>
                                      </body>
                                      </html>
                                      """;

  public string ConvertMarkdownToHtml(string markdown)
  {
    if (string.IsNullOrWhiteSpace(markdown))
      return string.Empty;

    try
    {
      var htmlContent = Markdig.Markdown.ToHtml(markdown, _pipeline);
      var fullHtml = HtmlTemplate.Replace("{{Content}}", htmlContent);
      _loggingService.LogOperation($"Converted markdown to HTML ({markdown.Length} chars)");
      return fullHtml;
    }
    catch (Exception ex)
    {
      _loggingService.LogOperation($"Error converting markdown to HTML: {ex.Message}");
      ErrorLogger.LogError(ex, "Markdown to HTML conversion");
      return $"<pre style=\"color:red;\">Error converting markdown to HTML: {ex.Message}</pre>";
    }
  }
}