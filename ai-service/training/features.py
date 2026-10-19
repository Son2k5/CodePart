import ast
import difflib
from sklearn.base import BaseEstimator, TransformerMixin
from sklearn.feature_extraction import DictVectorizer

def features(row):
    code = row.get('sourceCode', '')
    starter = row.get('starterCode', '')
    result = {'length': len(code), 'lines': len(code.splitlines()),
              'starterDiff': 1 - difflib.SequenceMatcher(None, code, starter).ratio(),
              'verdict=' + row.get('verdict', 'none'): 1,
              'language=' + row.get('language', 'python'): 1,
              'passRatio': row.get('passed', 0) / max(1, row.get('total', 0)),
              'failedSamples': row.get('failedSamples', 0)}
    if row.get('language', 'python') == 'python':
        try:
            tree = ast.parse(code)
            nodes = list(ast.walk(tree))
            stack = [(tree, 0)]
            depth = 0
            while stack:
                node, d = stack.pop(); depth = max(depth, d)
                stack.extend((child, d + 1) for child in ast.iter_child_nodes(node))
            stored = {n.id for n in nodes if isinstance(n, ast.Name) and isinstance(n.ctx, ast.Store)}
            loaded = {n.id for n in nodes if isinstance(n, ast.Name) and isinstance(n.ctx, ast.Load)}
            result.update(depth=depth, loops=sum(isinstance(n, (ast.For, ast.While)) for n in nodes),
                          branches=sum(isinstance(n, ast.If) for n in nodes), unused=len(stored-loaded))
        except (SyntaxError, ValueError, RecursionError): result['parseError'] = 1
    return result

class NumericFeatures(BaseEstimator, TransformerMixin):
    def fit(self, X, y=None):
        self.vectorizer_ = DictVectorizer().fit([features(x) for x in X])
        return self
    def transform(self, X): return self.vectorizer_.transform([features(x) for x in X])

class CodeText(BaseEstimator, TransformerMixin):
    def fit(self, X, y=None): return self
    def transform(self, X): return [x.get('sourceCode', '') for x in X]
