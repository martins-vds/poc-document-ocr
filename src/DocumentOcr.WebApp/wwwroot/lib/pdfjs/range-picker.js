// pdf.js wrapper for the in-page range picker.
// Loaded as an ES module via JSImport interop.
//
// Exposes loadDocument(arrayBuffer), renderPage(rendererId, pageNumber, canvasElement),
// and dispose(rendererId). All pdf.js-specific code lives in this module
// (Constitution III — encapsulate framework-specific code behind a clean surface).

const documents = new Map();
let nextId = 1;
let pdfjsLib = null;

async function ensurePdfJs() {
    if (pdfjsLib) return pdfjsLib;
    pdfjsLib = await import('./pdf.mjs');
    pdfjsLib.GlobalWorkerOptions.workerSrc = '/lib/pdfjs/pdf.worker.mjs';
    return pdfjsLib;
}

export async function loadDocument(arrayBuffer) {
    const lib = await ensurePdfJs();
    const data = new Uint8Array(arrayBuffer);
    try {
        const loadingTask = lib.getDocument({ data });
        const doc = await loadingTask.promise;
        const id = nextId++;
        documents.set(id, doc);
        return { rendererId: id, numPages: doc.numPages };
    } catch (err) {
        // FR-015: corrupt / encrypted / non-PDF
        return { rendererId: 0, numPages: 0, error: err?.message || 'Unable to read PDF' };
    }
}

export async function renderPage(rendererId, pageNumber, canvas) {
    const doc = documents.get(rendererId);
    if (!doc) return false;
    const page = await doc.getPage(pageNumber);
    const viewport = page.getViewport({ scale: 1.0 });

    // Fit width to canvas's bounding box while preserving aspect.
    const containerWidth = canvas.parentElement?.clientWidth || 400;
    const scale = containerWidth / viewport.width;
    const scaled = page.getViewport({ scale });

    canvas.width = scaled.width;
    canvas.height = scaled.height;
    const ctx = canvas.getContext('2d');
    await page.render({ canvasContext: ctx, viewport: scaled }).promise;
    return true;
}

export function dispose(rendererId) {
    const doc = documents.get(rendererId);
    if (doc) {
        try { doc.destroy(); } catch { /* ignore */ }
        documents.delete(rendererId);
    }
}
