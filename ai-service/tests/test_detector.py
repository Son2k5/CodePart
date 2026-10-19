import pytest
from app.contracts import RunState, Verdict
from app.stage.detector import detect

@pytest.mark.parametrize('code,action,verdict,passed,total,stage', [
    ('','none',None,0,0,'Start'), ('# start','none',None,0,0,'Start'),
    ('numbers = []','none',None,0,0,'InProgress'), ('','run',None,0,0,'InProgress'),
    ('print(1)','submit','WrongAnswer',0,15,'LogicError'),
    ('print(1)','submit','WrongAnswer',8,15,'PartialPass'),
    ('print(1)','run','CompileError',0,1,'CannotRun'),
    ('print(1)','run','RuntimeError',8,15,'CannotRun'),
    ('print(1)','submit','TimeLimitExceeded',0,15,'PartialPass'),
    ('print(1)','submit','MemoryLimitExceeded',0,15,'PartialPass'),
    ('print(1)','submit','Accepted',15,15,'InProgress'),
])
def test_stages(context,code,action,verdict,passed,total,stage):
    context.sourceCode=code
    context.runState=RunState(lastAction=action,passed=passed,total=total,verdicts=[] if not verdict else [Verdict(testCaseId='1',isSample=True,verdict=verdict)])
    assert detect(context).stage == stage

def test_starter_and_priority(context):
    context.sourceCode=context.problem.starterCode='numbers = list(map(int, input().split()))'
    assert detect(context).stage == 'Start'
    context.runState=RunState(compileError='SyntaxError',passed=8,total=15)
    assert detect(context, lambda c: ('WrongAlgorithm',.8)).stage == 'CannotRun'
    context.history.repeatedCodeCount=3
    assert detect(context).stuck

@pytest.mark.parametrize('verdict,category',[('SyntaxError','SyntaxError'),('IOFormat','IOFormat')])
def test_detailed_cannot_run(context,verdict,category):
    context.runState.verdicts=[Verdict(testCaseId='sample',isSample=True,verdict=verdict)]
    result=detect(context)
    assert result.stage=='CannotRun' and result.category==category
