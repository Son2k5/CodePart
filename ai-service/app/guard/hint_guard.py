import ast
import difflib
import keyword
import re

def normalize(code):
    # Language-independent token normalization also covers Java/C++ identifiers.
    return ' '.join('ID' if t.isidentifier() and not keyword.iskeyword(t) else t
                    for t in re.findall(r'\w+|[^\s\w]', code))

def structure(code):
    try:
        tree = ast.parse(code)
        for node in ast.walk(tree):
            if isinstance(node, ast.Name): node.id = 'ID'
            if isinstance(node, ast.arg): node.arg = 'ID'
            if isinstance(node, (ast.FunctionDef, ast.ClassDef)): node.name = 'ID'
        return ast.dump(tree, include_attributes=False)
    except (SyntaxError, ValueError, RecursionError):
        return normalize(code)

def violations(hint, context, stage, max_lines=3):
    text = '\n'.join([hint.message, *hint.steps, hint.guidingQuestion or ''])
    if len(text) > 12000:
        return ['response_size']
    reasons = []
    blocks = re.findall(r'```[^\n]*\n(.*?)```|~~~[^\n]*\n(.*?)~~~', text, re.S)
    snippets = [a or b for a, b in blocks]
    inline = re.findall(r'`([^`]+)`', text)
    # Also inspect unfenced code to avoid markdown-only protection.
    code_like = re.search(r'(?m)^\s*(?:def |class |for .+[:{]|while .+[:{]|#include|public static|\w+\s*=|print\(|return\s)', text)
    if stage in ('Start', 'InProgress') and (snippets or inline or code_like): reasons.append('early_code')
    if sum(len([l for l in s.splitlines() if l.strip()]) for s in snippets) > max_lines: reasons.append('code_length')
    if code_like and len(text.splitlines()) > max_lines: reasons.append('unfenced_code')
    if re.search(r'\binput\s*\(', text) and re.search(r'\bprint\s*\(', text) or re.search(r'\b(?:int|void)\s+main\s*\(|public\s+static\s+void\s+main', text):
        reasons.append('complete_program')
    ref = context.problem.referenceSolution
    if ref:
        reference = normalize(ref)
        candidates = snippets + inline + [text]
        if any(reference in normalize(s) or difflib.SequenceMatcher(None, structure(ref), structure(s)).ratio() >= .78 for s in candidates):
            reasons.append('reference_similarity')
    for v in context.runState.verdicts:
        if not v.isSample and any(value and value.strip() and value.strip() in text for value in (v.input, v.expected, v.actual)):
            reasons.append('hidden_test')
    if any(n < 1 or n > max(1, len(context.sourceCode.splitlines())) for n in hint.suspectedLines): reasons.append('invalid_line')
    return sorted(set(reasons))
