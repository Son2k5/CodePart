from typing import Literal
from uuid import UUID
from pydantic import BaseModel, ConfigDict, Field, model_validator

Stage = Literal['Start', 'InProgress', 'LogicError', 'CannotRun', 'PartialPass']

class Model(BaseModel):
    model_config = ConfigDict(extra='forbid', str_max_length=65536)

class TheoryPassage(Model):
    chunkId: UUID
    documentId: UUID
    blockId: UUID
    topicId: UUID
    classId: UUID
    revision: UUID
    version: int
    title: str
    page: int | None = None
    heading: str = ''
    content: str = Field(max_length=6000)
    score: float = 0

class Problem(Model):
    title: str
    statement: str
    constraints: str = ''
    language: str
    starterCode: str = ''
    referenceSolution: str | None = None

class Verdict(Model):
    testCaseId: str
    isSample: bool
    verdict: str
    input: str | None = None
    expected: str | None = None
    actual: str | None = None

class RunState(Model):
    lastAction: Literal['none', 'run', 'submit'] = 'none'
    compileError: str | None = None
    runtimeError: str | None = None
    verdicts: list[Verdict] = Field(default_factory=list, max_length=100)
    passed: int = Field(default=0, ge=0)
    total: int = Field(default=0, ge=0)
    runtimeMs: int = Field(default=0, ge=0)
    memoryKb: int = Field(default=0, ge=0)

    @model_validator(mode='after')
    def counts(self):
        if self.passed > self.total:
            raise ValueError('passed exceeds total')
        return self

class PreviousHint(Model):
    level: int = Field(ge=1, le=3)
    text: str = Field(max_length=6000)

class History(Model):
    previousHints: list[PreviousHint] = Field(default_factory=list, max_length=10)
    submitCount: int = Field(default=0, ge=0)
    minutesStuck: float = Field(default=0, ge=0)
    consecutiveFailures: int = Field(default=0, ge=0)
    repeatedCodeCount: int = Field(default=0, ge=0)

class Request(Model):
    type: Literal['AutoHint', 'Hint', 'TraceTestCase', 'ExplainError', 'Ask'] = 'Hint'
    level: int = Field(default=1, ge=1, le=3)
    question: str | None = Field(default=None, max_length=2000)

class AssistRequest(Model):
    requestId: str = Field(max_length=100)
    studentRef: str = Field(max_length=128)
    problem: Problem
    sourceCode: str
    runState: RunState = Field(default_factory=RunState)
    history: History = Field(default_factory=History)
    request: Request = Field(default_factory=Request)
    theory: list[TheoryPassage] = Field(default_factory=list, max_length=8)

class Hint(Model):
    message: str = Field(min_length=1, max_length=6000)
    steps: list[str] = Field(max_length=8)
    suspectedLines: list[int] = Field(max_length=10)
    guidingQuestion: str | None

class AssistResponse(Hint):
    stage: Stage
    stageConfidence: float = Field(ge=0, le=1)
    errorCategory: str
    level: int = Field(ge=1, le=3)
    guardStatus: Literal['ok', 'regenerated', 'fallback']
    modelVersion: str
    promptVersion: str
    tokensIn: int = Field(ge=0)
    tokensOut: int = Field(ge=0)

class ErrorResponse(Model):
    code: Literal['AI_UNAVAILABLE', 'INVALID_CONTEXT', 'GUARD_FALLBACK']
