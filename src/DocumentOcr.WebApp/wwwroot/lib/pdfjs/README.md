# pdf.js (vendored)

**Source**: https://github.com/mozilla/pdf.js  
**Version**: 4.7.76  
**License**: Apache-2.0 (see `LICENSE` in this folder)  
**Distribution**: prebuilt `pdf.mjs` + `pdf.worker.mjs` from
<https://cdn.jsdelivr.net/npm/pdfjs-dist@4.7.76/build/>

Used by `Components/Shared/PdfRangePicker.razor` (via the wrapper module
`range-picker.js`) for in-browser PDF preview and page-count probing on the
Upload page (feature 002-upload-page-range-selection).

## Update procedure

```bash
cd src/DocumentOcr.WebApp/wwwroot/lib/pdfjs
VERSION=4.7.76
curl -fsSL -o pdf.mjs        "https://cdn.jsdelivr.net/npm/pdfjs-dist@${VERSION}/build/pdf.mjs"
curl -fsSL -o pdf.worker.mjs "https://cdn.jsdelivr.net/npm/pdfjs-dist@${VERSION}/build/pdf.worker.mjs"
curl -fsSL -o LICENSE        "https://raw.githubusercontent.com/mozilla/pdf.js/v${VERSION}/LICENSE"
```

After updating, run `dotnet build src/DocumentOcr.WebApp` and exercise the
Upload page in a real browser to confirm the worker still loads and renders.
