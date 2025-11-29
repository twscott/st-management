<!--
Sync Impact Report:
- Version change: 1.0.0 → 1.1.0
- Modified principles: Added 5 new critical principles (VIII-XII)
- Added: Complete development lifecycle, LLM standardization, requirements validation, database schema design
- Templates requiring updates: ⚠ spec-template.md, plan-template.md need review
- Follow-up TODOs: None - all placeholders filled
-->

# AI Firstohm Agent Constitution

## Core Principles

### I. Design-First Development (NON-NEGOTIABLE)
**MUST** create and approve design documents before any implementation:
- Every feature MUST have a `design_vX.md` in `.specify/memory/features/` before coding
- Design documents are the **Single Source of Truth** - no deviation allowed
- Function names, parameters, return types, and error handling MUST match design exactly
- Design and implementation MUST be committed separately (Stage Gate enforcement)
- Any ambiguity or missing specification MUST trigger immediate design review, not guesswork

**Rationale**: Previous project failures stemmed from "vibe coding" - implementing without clear specifications. This principle prevents scope creep and ensures predictable outcomes.

### II. Sandbox Isolation (NON-NEGOTIABLE)
**MUST** use sandbox environment for all experimental and draft work:
- ALL experiments, drafts, and untested code MUST reside in `Sandbox/` directory
- `Scripts/`, `Tests/`, and `src/` directories contain FINAL versions ONLY
- Code promotion flow: `Sandbox/` → User Approval → `Scripts/`/`Tests/`/`src/` → Git
- NO versioned filenames allowed (`_v2`, `_v3`, `_tmp`, `_final`, `_new`, `_old`, `_backup`)
- Sandbox directory excluded from Git via `.gitignore`

**Rationale**: Mixing experimental code with production code caused multiple project restarts. Clear isolation prevents contamination of stable codebase.

### III. Gatekeeper Quality Control (NON-NEGOTIABLE)
**MUST** pass automated quality gates before merge:
- File naming compliance: NO `_v2`, `_tmp`, `_final` patterns
- Design-code separation: Design docs and implementation in separate commits
- Documentation requirement: Scripts in `Scripts/` MUST have corresponding `Docs/Scripts/{name}.md`
- Test coverage: Every function in `src/` MUST have corresponding `test_*.py` with ≥95% coverage
- Tech debt control: Net additions ≤ deletions + 50 lines per commit

**Rationale**: Automated enforcement prevents human oversight errors and maintains consistent quality standards across all contributors (human and AI).

### IV. Incremental Development with Checkpoints (NON-NEGOTIABLE)
**MUST** pause and confirm after each significant step:
- Stop after creating any new file for user review
- Stop after completing each function for testing
- Stop before moving files from Sandbox to production directories
- Stop when encountering uncertainty or design conflicts
- NO batching multiple changes without intermediate confirmations

**Rationale**: AI coding agents historically "run away" with implementations, making multiple unvetted changes. Checkpoints give users control and prevent cascading errors.

### V. Dual LLM Architecture for Privacy
**MUST** route operations based on data sensitivity:
- Local LLM (7B): De-identification, quick decisions, privacy-critical operations
- Cloud LLM (DeepSeek): Complex reasoning, OCR parsing, analytical tasks
- Personal data (names, IDs, contact info) MUST be masked before Cloud LLM processing
- Clarification Engine MUST NOT guess - multi-turn dialogue to collect parameters
- All LLM interactions logged for RAG training and audit trails

**Rationale**: Company operates in privacy-sensitive domain. Architecture ensures compliance while leveraging powerful cloud models for complex tasks.

### VI. Test-Driven Development (NON-NEGOTIABLE)
**MUST** write tests before implementation:
- Unit tests written and approved by user BEFORE writing function code
- Tests MUST fail initially (Red), then implementation makes them pass (Green)
- Test coverage ≥95% for all functions in `src/`
- Tests MUST cover: normal cases, boundary conditions, error handling, edge cases
- Integration tests required for: module interactions, API contracts, data flows

**Rationale**: TDD ensures code correctness and prevents regressions. Historical pattern showed untested code led to cascading failures and restarts.

### VII. Modular Architecture and Reusability
**MUST** design for modularity and avoid duplication:
- Single function ≤200 lines (exceptions require explicit reviewer approval)
- Code reuse rate ≥70% - prefer existing functions over new implementations
- Each module MUST be independently testable and documentable
- Clear interfaces with explicit input/output contracts
- `function_map.md` MUST stay synchronized with actual codebase

**Rationale**: Monolithic code and duplication made previous iterations unmaintainable. Modular design enables independent testing and gradual system evolution.

### VIII. Complete Development Lifecycle (NON-NEGOTIABLE)
**MUST** follow the complete development lifecycle before production deployment:
1. **Analysis Phase**: Requirements gathering and clarification with user
2. **Design Phase**: Design document creation with user approval
3. **Test Document Phase**: Write test specifications before implementation
4. **Sandbox Development Phase**: Implement in `Sandbox/` directory
5. **Unit Testing Phase**: Run and pass all unit tests (≥95% coverage)
6. **Integration Testing Phase**: Test module interactions and system integration
7. **Sandbox Exit Testing Phase**: Final validation before production promotion
8. **Production Deployment**: Move to `src/`/`Scripts/`/`Tests/` directories

**Exit Criteria for Sandbox**:
- All unit tests pass with ≥95% coverage
- All integration tests pass
- Design document matches implementation
- User approval obtained for sandbox exit
- Documentation complete and reviewed

**Rationale**: A complete and structured lifecycle prevents premature deployment of untested code. Each phase has clear entry and exit criteria, ensuring quality at every stage.

### IX. LLM Adapter Factory Standardization (NON-NEGOTIABLE)
**MUST** use LLM Adapter Factory for ALL LLM interactions:
- Client-side LLM calls MUST go through LLM Adapter Factory
- Server-side LLM calls MUST go through LLM Adapter Factory
- NO direct LLM API calls allowed (Ollama, DeepSeek, or any other provider)
- Factory handles: routing, error handling, retry logic, logging, monitoring
- Factory enforces: privacy rules, de-identification, usage tracking

**Factory Responsibilities**:
- Route to appropriate LLM (Local/Cloud) based on operation type
- Apply data masking before Cloud LLM calls
- Log all interactions for audit and RAG training
- Handle failures and fallback mechanisms
- Provide unified interface regardless of underlying provider

**Rationale**: Centralized LLM access ensures consistent privacy protection, monitoring, and maintainability. Direct API calls bypass critical safeguards and create technical debt.

### X. Requirements Validation and Discussion (NON-NEGOTIABLE)
**MUST** validate and discuss ALL requirements before design:
- User requirements may contain errors, ambiguities, or conflicts
- Design specifications from users may have gaps or inconsistencies
- NEVER accept requirements or designs at face value
- MUST engage in clarification dialogue with user before finalizing
- Document all clarification questions and answers
- Present refined requirements back to user for confirmation

**Validation Process**:
1. Receive initial requirement or design specification
2. Analyze for: ambiguities, conflicts, missing details, technical feasibility
3. Generate clarification questions
4. Discuss with user until all uncertainties resolved
5. Document final agreed requirements
6. Obtain explicit user approval before design phase

**Rationale**: Even user-provided requirements and designs can be flawed. Proactive validation prevents building the wrong solution and costly rework later.

### XI. Concise Conclusion for User Confirmation (NON-NEGOTIABLE)
**MUST** provide concise summary before generating full documentation:
- After requirements discussion reaches conclusion
- MUST present brief, clear summary of agreed requirements
- Summary should be 3-5 bullet points or 1 short paragraph
- User MUST explicitly confirm summary before full document generation
- Only proceed to detailed documentation after confirmation received

**Summary Format**:
```
需求結論確認：
1. [核心功能點1]
2. [核心功能點2]
3. [核心技術決策]
4. [關鍵約束條件]

請確認以上理解是否正確，確認後我將產生完整的設計文件。
```

**Rationale**: Detailed documents take time to create and review. A concise summary catch misunderstandings early, saving time and ensuring alignment before investment in full documentation.

### XII. Database Schema Design Documentation (NON-NEGOTIABLE)
**MUST** create dedicated database schema documentation:
- Design specifications involving database MUST include separate schema section
- Schema documentation MUST be in standalone, clearly labeled section or file
- Schema MUST clearly express: table names, column names, data types, constraints, indexes, relationships
- User MUST review and approve schema design BEFORE implementation
- Schema changes require explicit user approval and migration plan

**Schema Documentation Requirements**:
- Table definitions with all columns and types
- Primary keys and foreign keys explicitly marked
- Indexes and their purposes documented
- Constraints (UNIQUE, NOT NULL, CHECK, etc.)
- Relationships between tables (1:1, 1:N, N:M) with diagrams if complex
- Sample data format for clarity
- Migration strategy if modifying existing schema

**Schema Review Checklist**:
- [ ] All tables and columns clearly named
- [ ] Data types appropriate for use cases
- [ ] Indexes optimize expected queries
- [ ] Foreign key relationships correctly defined
- [ ] Constraints prevent invalid data
- [ ] User has approved schema design

**Rationale**: Database schema errors are expensive to fix after implementation. Dedicated schema documentation ensures thorough review and approval, preventing costly migrations and data issues.

### XIII. Use Case Lifecycle Management via spec-kit (NON-NEGOTIABLE)
**MUST** use spec-kit for ALL use case lifecycle management:
- ALL new use cases MUST be documented in `.specify/memory/features/` using spec-kit templates
- Use case documentation MUST follow spec-kit conventions: `{number}-{feature-name}.md` format
- Use case status tracking: `planned` → `in-progress` → `testing` → `completed` → `deployed`
- NEVER create use cases outside spec-kit management system
- Use case changes require spec-kit documentation update BEFORE implementation
- spec-kit serves as single source of truth for all feature specifications

**spec-kit Commands for UC Lifecycle**:
- `specify init` - Initialize new use case documentation
- `specify check` - Validate use case documentation completeness
- `specify status` - Check current use case implementation status
- `specify version` - Track use case version history

**Use Case Documentation Requirements**:
- Clear UC identifier (e.g., UC-Phase2-05)
- Business requirements and acceptance criteria
- Technical specifications and constraints
- Dependencies on other use cases
- Test coverage requirements (≥95%)
- Deployment and rollback procedures

**UC Approval Workflow**:
1. Create UC spec using spec-kit template in `.specify/memory/features/`
2. User reviews and approves UC specification
3. Link UC to design document (`design_vX.md`)
4. Implement following complete development lifecycle (Principle VIII)
5. Update UC status in spec-kit upon completion
6. Archive completed UC documentation for historical reference

**Rationale**: Scattered use case documentation and ad-hoc feature additions caused tracking difficulties and scope creep. spec-kit provides centralized, version-controlled, auditable use case management ensuring all features are properly specified, approved, and tracked from conception to deployment.

## Technical Standards

### Technology Stack
- **Python**: 3.11+ (production), 3.13.9 (development)
- **Backend**: FastAPI, Uvicorn
- **Databases**: PostgreSQL (core data), MySQL (legacy integration), Redis (caching)
- **LLMs**:
  - Local: Ollama (http://192.168.1.24:11434)
  - Cloud: DeepSeek API (https://platform.deepseek.com/)
- **Frontend**: Browser-based UI (HTML/CSS/JavaScript), Bootstrap 5
- **Communication**: REST API, WebSocket (real-time updates)
- **Testing**: pytest, coverage.py

### Code Quality Standards
- Linting MUST pass for all languages (Python: pylint/ruff)
- NO hardcoded credentials - use `.env` or configuration files
- Structured logging required for all modules
- Error messages MUST be informative and actionable
- API versioning: `/api/v1/...` format

### LLM Integration Standards (Constitution IX)
- **ALL** LLM calls MUST use LLM Adapter Factory
- NO direct API calls to Ollama, DeepSeek, or any LLM provider
- Factory location: `src/core/llm_adapter_factory.py` (or equivalent)
- Client code: `from core.llm_adapter_factory import get_llm_adapter`
- Server code: Same factory, different routing configuration

### Security Requirements
- Personal data de-identification before cloud processing
- Test data MUST be sanitized (no real customer information)
- API keys and tokens in environment variables only
- HTTPS for all external communications
- Authentication required for privileged operations

### Performance Standards
- Client agent footprint <100MB
- API response time <500ms for standard operations
- OCR processing <30s per page
- WebSocket updates <100ms latency
- Background job queue prevents UI blocking

## Development Workflow

### Phase Sequence (AI_Flow_Rule_v1.0 + Constitution VIII)
**MUST** follow this exact sequence:
1. **Analysis Phase**: Requirements discussion, clarification, concise conclusion approval
2. **Design Phase**: Create `design_vX.md` (including schema if DB involved), define functions, obtain user approval
3. **Test Document Phase**: Write test specifications and expected behaviors
4. **Sandbox Development Phase**: Implement in `Sandbox/` following approved design
5. **Unit Testing Phase**: Execute all unit tests, achieve ≥95% coverage
6. **Integration Testing Phase**: Test module interactions, API contracts, data flows
7. **Sandbox Exit Testing Phase**: Final validation, user approval for promotion
8. **Production Deployment**: Move to `src/`/`Scripts/`/`Tests/`, commit to Git
9. **Review Phase**: Gatekeeper validation, peer review, design conformance check
10. **Release Phase**: Tag, changelog, CI green, merge to main

### Commit Standards
- Atomic commits: One logical change per commit
- Design and code NEVER in same commit
- Commit message format: `type(scope): description`
  - Types: `feat`, `fix`, `docs`, `test`, `refactor`, `chore`
  - Example: `feat(ocr): add multi-page PDF processing`
- All commits MUST pass Gatekeeper checks

### Documentation Requirements
- Every script in `Scripts/` has `Docs/Scripts/{name}.md`
- Every feature has design document in `.specify/memory/features/`
- `function_map.md` updated with every source file change
- Session summaries in `Docs/Todo/YYYY-MM-DD_*.md` for significant work
- README.md kept current with project status

### Code Review Process
- Design review before implementation approval
- Gatekeeper automated checks (non-negotiable)
- Human review for:
  - Architectural changes
  - New modules or APIs
  - Breaking changes
  - Complex algorithms
- Review feedback MUST be addressed before merge

## Governance

### Constitutional Authority
This constitution supersedes all other development practices and guidelines. In case of conflicts:
1. Constitution (this document)
2. AI_Flow_Rule_v1.0.md (phase-specific rules)
3. AI_Coding_Phase_Rule_v1.0.md (implementation details)
4. .copilot-instructions.md (AI assistant guidance)

### Compliance Enforcement
- All PRs/commits MUST verify compliance via Gatekeeper
- Complexity additions MUST be justified in design documents
- Violations result in automatic commit rejection
- Repeated violations trigger design review

### Amendment Process
1. Propose amendment with rationale and impact analysis
2. Document affected systems and migration requirements
3. Obtain approval from maintainers
4. Update dependent templates and documentation
5. Increment version per semantic versioning:
   - **MAJOR**: Breaking changes, principle removal/redefinition
   - **MINOR**: New principles, expanded guidance
   - **PATCH**: Clarifications, typo fixes, wording improvements

### Runtime Guidance
- AI assistants: Follow `.copilot-instructions.md` for operational rules
- Human developers: Reference `Docs/PROJECT_KNOWLEDGE_MAP.md` for context
- New contributors: Read `README.md` and this constitution first
- Emergency procedures: Contact maintainers via Git issues

**Version**: 1.2.0 | **Ratified**: 2025-11-24 | **Last Amended**: 2025-11-24
