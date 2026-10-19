import hashlib
import json
from pathlib import Path
import joblib

class Classifier:
    def __init__(self, path):
        root = Path(path)
        metadata = json.loads((root/'metadata.json').read_text())
        if hashlib.sha256((root/'model.joblib').read_bytes()).hexdigest() != metadata['modelSha256']:
            raise ValueError('Model hash mismatch')
        # Only deploy trusted, locally trained artifacts: joblib is not an untrusted format.
        models = joblib.load(root/'model.joblib')
        self.model = models['errorCategory']
        self.stage_model = models['stage']
        self.version = metadata['version']
    def __call__(self, c):
        row = self.row(c)
        probabilities = self.model.predict_proba([row])[0]
        index = probabilities.argmax()
        return str(self.model.classes_[index]), float(probabilities[index])

    @staticmethod
    def row(c):
        return {'sourceCode':c.sourceCode,'starterCode':c.problem.starterCode,'language':c.problem.language,
               'passed':c.runState.passed,'total':c.runState.total,'verdict':next((v.verdict for v in c.runState.verdicts if v.verdict != 'Accepted'),'none')}

    def stage_confidence(self, c, stage):
        probabilities = self.stage_model.predict_proba([self.row(c)])[0]
        return dict(zip(self.stage_model.classes_, probabilities)).get(stage, 0.0)
