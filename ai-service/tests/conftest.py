import pytest
from app.contracts import AssistRequest

@pytest.fixture
def context():
    return AssistRequest.model_validate({'requestId':'test','studentRef':'anon','problem':{'title':'Tổng','statement':'Tính tổng các số','language':'python'},'sourceCode':''})
