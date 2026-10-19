FROM python:3.11-slim
WORKDIR /service
COPY pyproject.toml ./
COPY uv.lock ./
COPY app ./app
COPY training ./training
RUN pip install --no-cache-dir uv==0.13.1 && uv sync --locked --no-dev --no-cache && useradd --uid 10001 --create-home assistant
ENV PATH="/service/.venv/bin:$PATH"
USER assistant
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000", "--limit-concurrency", "32"]
