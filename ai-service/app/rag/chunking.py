"""Bounded chunks with source positions. No code fence is silently split."""
import re

MAX_CHARS = 6000
TARGET_CHARS = 1800


def markdown_chunks(text):
    heading = ''
    buffer = []
    fence = None
    size = 0
    for line in text.replace('\r\n', '\n').splitlines(keepends=True):
        marker = re.match(r'^\s{0,3}(`{3,}|~{3,})', line)
        if not fence and (line.startswith('#') or size >= TARGET_CHARS):
            if buffer and ''.join(buffer).strip():
                yield heading, ''.join(buffer).strip()
            buffer, size = [], 0
        if not fence and re.match(r'^#{1,6}\s', line):
            heading = line.strip().lstrip('#').strip()[:250]
        if marker:
            token = marker.group(1)
            if fence is None:
                fence = token
            elif token[0] == fence[0] and len(token) >= len(fence):
                fence = None
        # Very long individual lines/code blocks need an author edit, not truncation.
        buffer.append(line)
        size += len(line)
        if size > MAX_CHARS:
            raise ValueError('CHUNK_TOO_LARGE')
    if buffer and ''.join(buffer).strip():
        yield heading, ''.join(buffer).strip()


def pdf_pages(data):
    from io import BytesIO
    from pypdf import PdfReader
    reader = PdfReader(BytesIO(data))
    if reader.is_encrypted:
        raise ValueError('PDF_ENCRYPTED')
    if len(reader.pages) > 500:
        raise ValueError('PDF_TOO_MANY_PAGES')
    extracted = []
    total = 0
    for number, page in enumerate(reader.pages, 1):
        text = page.extract_text() or ''
        total += len(text)
        if total > 2_000_000:
            raise ValueError('DOCUMENT_TOO_LARGE')
        if text.strip():
            extracted.append((number, text))
    if not extracted:
        raise ValueError('PDF_NEEDS_OCR')
    return extracted
