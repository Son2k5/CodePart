Layered FastAPI application organized under `app/`:
- `main.py` is the entry point: sets up lifespan (httpx client, ProviderClient, RagService, Redis cache, optional ML classifier), registers global exception handlers (`INVALID_CONTEXT`, `AI_UNAVAILABLE`), authenticates via `X-Service-Key` header, and mounts `/v1/assist` plus the RAG router.
- `api/pipeline.py` implements the core hint-generation loop: stage detection → prompt build → LLM call → guard validation → retry with stricter prompt → fallback message; integrates Redis caching keyed on request + detector output + model identity.
- `stage/detector.py` contains a rule-based classifier (with optional scikit-learn `Classifier` override) that maps run state / verdicts into a `Stage` (`Start|InProgress|LogicError|CannotRun|PartialPass`) and category.
- `hints/prompts.py` renders Jinja2 system/user prompts from `prompts/system.jinja` and `prompts/strategies.yaml`; `guard/hint_guard.py` validates outputs against policy rules before returning.
- `llm/client.py` abstracts provider calls behind `ProviderClient.complete()`.
- `rag/` is a self-contained sub-app: `routes.py` exposes `/internal/rag/{index,delete,search,answer,draft}` with per-endpoint timeouts and strict source-id allowlists; `service.py` + `chunking.py` handle PDF ingestion and chunking; `contracts.py` defines RAG Pydantic models.
- `cache/redis_cache.py` provides async Redis key/value access used by both pipeline and RAG.
- Contracts live in `app/contracts.py` (Pydantic v2 models for requests/responses).
- Optional extras: `training/` scripts (distill, finetune, lora, prepare) and `eval/` harness are separate tooling packages, not part of the runtime server.