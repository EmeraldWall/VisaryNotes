# Test fixtures

`sample.docx`, `sample.doc` and `sample.pdf` were made from `sample.source.html` with LibreOffice 24 on Linux:

```
soffice --headless --convert-to odt sample.source.html
soffice --headless --convert-to "docx:MS Word 2007 XML" sample.source.odt
soffice --headless --convert-to "doc:MS Word 97" sample.source.odt
soffice --headless --convert-to pdf sample.source.odt
```

The source has a title, two headings, bold, italic and underlined text, a link, a bulleted and a numbered list, a table, an embedded PNG and a non-ASCII word (café).
