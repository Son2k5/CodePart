from dataclasses import dataclass
from app.contracts import AssistRequest, Stage

@dataclass
class Detection:
    stage: Stage
    confidence: float
    category: str
    stuck: bool

def detect(c: AssistRequest, classifier=None) -> Detection:
    r = c.runState
    v = {x.verdict.replace(' ', '') for x in r.verdicts}
    error = (r.compileError or '') + (r.runtimeError or '')
    category = next((k for k in ['SyntaxError', 'NameError', 'TypeError', 'IOFormat'] if k in error), 'Unknown')
    if error or v & {'CompileError', 'RuntimeError', 'SyntaxError', 'IOFormat'}:
        stage = 'CannotRun'
        if category == 'Unknown':
            category = 'IOFormat' if 'IOFormat' in v else 'SyntaxError' if r.compileError or v & {'CompileError', 'SyntaxError'} else 'RuntimeError'
    elif 0 < r.passed < r.total or v & {'TimeLimitExceeded', 'MemoryLimitExceeded', 'TLE', 'MLE'}:
        stage, category = 'PartialPass', 'Complexity' if v & {'TimeLimitExceeded', 'MemoryLimitExceeded', 'TLE', 'MLE'} else 'MissingEdgeCase'
    elif 'WrongAnswer' in v:
        stage, category = 'LogicError', 'WrongAlgorithm'
    elif r.lastAction == 'none' and (c.sourceCode.strip() == c.problem.starterCode.strip() or len(c.sourceCode.strip()) < 8):
        stage = 'Start'
    else:
        stage = 'InProgress'
    confidence = 1.0
    # ML may refine the category/confidence, but cannot override rule priority.
    if classifier is not None:
        predicted, category_confidence = classifier(c)
        if hasattr(classifier, 'stage_confidence'):
            confidence = float(classifier.stage_confidence(c, stage))
        if stage in ('LogicError', 'PartialPass') and category_confidence >= .65:
            category = predicted
    h = c.history
    return Detection(stage, confidence, category, h.minutesStuck >= 5 or h.consecutiveFailures >= 3 or h.repeatedCodeCount >= 3)
