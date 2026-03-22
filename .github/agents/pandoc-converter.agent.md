---
description: "Convert .docx to Markdown via pandoc MCP. Trigger words: docx, Word, convert, markdown."
tools: [read, search, pandoc/*]
---
Find `doc` folders (user-specified or scan workspace), call `mcp_pandoc_convert_docx_to_markdown` for each. Report results.

- "list"/"preview"/"dry run" → use `mcp_pandoc_list_docx_files` instead.
- "force"/"reconvert" → pass `force: true`.
- Never modify/delete original `.docx` files.
4. Summarize the conversion results per folder.

## Output Format
A brief summary listing each file and its conversion status (OK/SKIP/FAIL).
