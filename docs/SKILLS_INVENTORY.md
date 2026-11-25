# Skill inventory

Found 296 existing skills and installed 10 additional .NET skills.
Checked name and description frontmatter fields and found no duplicate names.
Compared every installed file with the staged source by SHA-256. The selected directories contain Markdown files and references.
Checked 36 relative documentation links in the installed skills.

## Official .NET skills

Staged upstream commit [656bed17226a](https://github.com/dotnet/skills/tree/656bed17226aa8ba6d78318fed0bea03f63addd9).
Copied complete skill folders and the MIT license to D:/ReactJS/.agents/skills/dotnet.
The upstream README documents individual skill installation and Codex plugins. This project uses the requested workspace folder and keeps direct paths in AGENTS.md.

Codex CLI 0.159.3 app-server reported all ten nested skills when queried with cwd D:/ReactJS. The standalone MiniErp cwd returned no parent workspace skills, so open D:/ReactJS as the VS Code workspace or use the paths in MiniErp/AGENTS.md. Start a new Codex conversation to refresh its startup inventory. Build and test work verified use of the installed documents by path.

| Skill | Function | Upstream path |
| --- | --- | --- |
| analyzing-dotnet-performance | Scans .NET code for ~50 performance anti-patterns across async, memory, strings, collections, LINQ, regex, serialization, and I/O with tiered severity… | [plugins/dotnet-diag/skills/analyzing-dotnet-performance/SKILL.md](https://github.com/dotnet/skills/blob/656bed17226aa8ba6d78318fed0bea03f63addd9/plugins/dotnet-diag/skills/analyzing-dotnet-performance/SKILL.md) |
| assertion-quality | Analyze assertion quality, depth, variety, and false confidence in existing tests. ALWAYS USE when asked about weak, shallow, trivial, always-true, self-… | [plugins/dotnet-test/skills/assertion-quality/SKILL.md](https://github.com/dotnet/skills/blob/656bed17226aa8ba6d78318fed0bea03f63addd9/plugins/dotnet-test/skills/assertion-quality/SKILL.md) |
| convert-to-cpm | Convert .NET projects and solutions (.sln, .slnx) to NuGet Central Package Management (CPM) using Directory.Packages.props. USE FOR: converting to CPM,… | [plugins/dotnet-nuget/skills/convert-to-cpm/SKILL.md](https://github.com/dotnet/skills/blob/656bed17226aa8ba6d78318fed0bea03f63addd9/plugins/dotnet-nuget/skills/convert-to-cpm/SKILL.md) |
| create-datadriven-aspnetcore | Generate or scaffold ASP.NET Core code — Razor Pages, Blazor components, MVC controllers, views, and Minimal API endpoints — without using ASP.NET Core… | [plugins/dotnet-data/skills/create-datadriven-aspnetcore/SKILL.md](https://github.com/dotnet/skills/blob/656bed17226aa8ba6d78318fed0bea03f63addd9/plugins/dotnet-data/skills/create-datadriven-aspnetcore/SKILL.md) |
| csharp-expert | Route C# and .NET requests to the exact installed specialist or smallest dotnet/skills marketplace plugin. USE FOR: "which specialist should own this" or… | [plugins/dotnet/skills/csharp-expert/SKILL.md](https://github.com/dotnet/skills/blob/656bed17226aa8ba6d78318fed0bea03f63addd9/plugins/dotnet/skills/csharp-expert/SKILL.md) |
| directory-build-organization | USE ONLY for (1) two or more projects with repeated MSBuild policy, targets, or package versions, or (2) an existing Directory.Build.props/targets/rsp… | [plugins/dotnet-msbuild/skills/directory-build-organization/SKILL.md](https://github.com/dotnet/skills/blob/656bed17226aa8ba6d78318fed0bea03f63addd9/plugins/dotnet-msbuild/skills/directory-build-organization/SKILL.md) |
| dotnet-webapi | Guides creation and modification of ASP.NET Core Web API endpoints with correct HTTP semantics, OpenAPI metadata, and error handling. USE FOR: adding new… | [plugins/dotnet-aspnetcore/skills/dotnet-webapi/SKILL.md](https://github.com/dotnet/skills/blob/656bed17226aa8ba6d78318fed0bea03f63addd9/plugins/dotnet-aspnetcore/skills/dotnet-webapi/SKILL.md) |
| msbuild | Use before MSBuild custom-target or incremental-generation repairs, XML/import/hook review, .binlog capture/replay, or assessing supported/unsupported… | [plugins/dotnet/skills/msbuild/SKILL.md](https://github.com/dotnet/skills/blob/656bed17226aa8ba6d78318fed0bea03f63addd9/plugins/dotnet/skills/msbuild/SKILL.md) |
| optimizing-ef-core-queries | Review and optimize application data access that uses Entity Framework Core (EF Core). Use for EF Core performance and correctness reviews of services,… | [plugins/dotnet-data/skills/optimizing-ef-core-queries/SKILL.md](https://github.com/dotnet/skills/blob/656bed17226aa8ba6d78318fed0bea03f63addd9/plugins/dotnet-data/skills/optimizing-ef-core-queries/SKILL.md) |
| run-tests | ALWAYS USE before running .NET tests or answering with a test command or flags. Trigger on "run the tests", "exact dotnet test command", one… | [plugins/dotnet-test/skills/run-tests/SKILL.md](https://github.com/dotnet/skills/blob/656bed17226aa8ba6d78318fed0bea03f63addd9/plugins/dotnet-test/skills/run-tests/SKILL.md) |

## Selection

- Used .NET ASP.NET Core and test skills for API contracts and xUnit execution.
- Skipped .NET Advanced skills because publishing, P/Invoke and SIMD are outside the current implementation.
- Skipped UI runtime, .NET 11, AI and upgrade plugins because the project is a new .NET 10 Web API and React app.
- Reviewed the managedcode, Aaronontheweb and novotnyllc repositories as candidates. The official set covers the present work, so no supplemental skills were installed.
- Did not inspect or activate skills-quarantine.

## Existing skills

### ai-data-and-automation (20)

| Name | Function | Path under .agents/skills |
| --- | --- | --- |
| agency-agentic-search-optimizer | Expert in WebMCP readiness and agentic task completion — audits whether AI agents can actually accomplish tasks on your site… | ai-data-and-automation/agency-agentic-search-optimizer/SKILL.md |
| agency-agents-orchestrator | Autonomous pipeline manager that orchestrates the entire development workflow. You are the leader of this process. | ai-data-and-automation/agency-agents-orchestrator/SKILL.md |
| agency-ai-data-remediation-engineer | Specialist in self-healing data pipelines — uses air-gapped local SLMs and semantic clustering to automatically detect,… | ai-data-and-automation/agency-ai-data-remediation-engineer/SKILL.md |
| agency-ai-engineer | Expert AI/ML engineer specializing in machine learning model development, deployment, and integration into production… | ai-data-and-automation/agency-ai-engineer/SKILL.md |
| agency-analytics-reporter | Expert data analyst transforming raw data into actionable business insights. Creates dashboards, performs statistical… | ai-data-and-automation/agency-analytics-reporter/SKILL.md |
| agency-automation-governance-architect | Governance-first architect for business automations (n8n-first) who audits value, risk, and maintainability before… | ai-data-and-automation/agency-automation-governance-architect/SKILL.md |
| agency-autonomous-optimization-architect | Intelligent system governor that continuously shadow-tests APIs for performance while enforcing strict financial and… | ai-data-and-automation/agency-autonomous-optimization-architect/SKILL.md |
| agency-data-consolidation-agent | AI agent that consolidates extracted sales data into live reporting dashboards with territory, rep, and pipeline summaries | ai-data-and-automation/agency-data-consolidation-agent/SKILL.md |
| agency-data-engineer | Expert data engineer specializing in building reliable data pipelines, lakehouse architectures, and scalable data… | ai-data-and-automation/agency-data-engineer/SKILL.md |
| agency-data-visualization-engineer | Expert data visualization engineer — chart-type selection by data and question, perceptually honest encodings, colorblind-… | ai-data-and-automation/agency-data-visualization-engineer/SKILL.md |
| agency-email-intelligence-engineer | Expert in extracting structured, reasoning-ready data from raw email threads for AI agents and automation systems | ai-data-and-automation/agency-email-intelligence-engineer/SKILL.md |
| agency-llm-post-training-engineer | Evidence-driven owner for SFT, preference optimization, RLHF/RLVR, MoE post-training, and the release gates that turn a… | ai-data-and-automation/agency-llm-post-training-engineer/SKILL.md |
| agency-model-qa-specialist | Independent model QA expert who audits ML and statistical models end-to-end - from documentation review and data… | ai-data-and-automation/agency-model-qa-specialist/SKILL.md |
| agency-multi-agent-systems-architect | Systems architect specializing in the design, coordination, and governance of multi-agent AI pipelines — covering topology… | ai-data-and-automation/agency-multi-agent-systems-architect/SKILL.md |
| agency-prompt-engineer | Specialist in crafting, testing, and systematically optimizing prompts for LLMs — turning vague instructions into reliable,… | ai-data-and-automation/agency-prompt-engineer/SKILL.md |
| agency-rag-pipeline-engineer | Production RAG specialist focused on chunking strategy, retrieval quality, hybrid search, re-ranking, and eval-driven… | ai-data-and-automation/agency-rag-pipeline-engineer/SKILL.md |
| agency-search-query-analyst | Specialist in search term analysis, negative keyword architecture, and query-to-intent mapping. Turns raw search query data… | ai-data-and-automation/agency-search-query-analyst/SKILL.md |
| agency-search-relevance-engineer | Expert search engineer for Elasticsearch and OpenSearch — index and analyzer design, BM25 query tuning, hybrid… | ai-data-and-automation/agency-search-relevance-engineer/SKILL.md |
| agency-test-automation-engineer | Expert end-to-end test automation engineer for Playwright and Cypress — resilient selectors, flake elimination, isolated… | ai-data-and-automation/agency-test-automation-engineer/SKILL.md |
| agency-voice-ai-integration-engineer | Expert in building end-to-end speech transcription pipelines using Whisper-style models and cloud ASR services — from raw… | ai-data-and-automation/agency-voice-ai-integration-engineer/SKILL.md |

### audio-speech (4)

| Name | Function | Path under .agents/skills |
| --- | --- | --- |
| iflytek-hyper-tts | Use when user asks to synthesize speech, convert text to audio, or read text aloud. 讯飞超拟人语音合成 -… | audio-speech/iflytek-hyper-tts/SKILL.md |
| iflytek-speed-transcription | Ultra-fast speech transcription using iFLYTEK Speed Transcription API. Transcribe audio files (WAV/PCM/MP3) up to 5 hours in… | audio-speech/iflytek-speed-transcription/SKILL.md |
| iflytek-voiceclone-tts | Use when user asks to clone a voice, train a custom voice model, or synthesize speech with a cloned voice. iFlytek Voice… | audio-speech/iflytek-voiceclone-tts/SKILL.md |
| real-time-voice-translator | Set up, run, configure, and troubleshoot the RakeshBabuGajula real-time voice translator: a local Streamlit application that… | audio-speech/real-time-voice-translator/SKILL.md |

### banner-design (1)

| Name | Function | Path under .agents/skills |
| --- | --- | --- |
| banner-design | Design banners for social media, ads, website heroes, creative assets, and print. Multiple art direction options with AI-… | banner-design/SKILL.md |

### brand (1)

| Name | Function | Path under .agents/skills |
| --- | --- | --- |
| brand | Brand voice, visual identity, messaging frameworks, asset management, brand consistency. Activate for branded content, tone… | brand/SKILL.md |

### business-finance-and-operations (10)

| Name | Function | Path under .agents/skills |
| --- | --- | --- |
| agency-accounts-payable-agent | Autonomous payment processing specialist that executes vendor payments, contractor invoices, and recurring bills across any… | business-finance-and-operations/agency-accounts-payable-agent/SKILL.md |
| agency-bookkeeper-controller | Expert bookkeeper and controller specializing in day-to-day accounting operations, financial reconciliations, month-end… | business-finance-and-operations/agency-bookkeeper-controller/SKILL.md |
| agency-chief-financial-officer | Strategic finance executive who governs capital allocation, treasury operations, financial planning, M&A finance, investor… | business-finance-and-operations/agency-chief-financial-officer/SKILL.md |
| agency-finance-tracker | Expert financial analyst and controller specializing in financial planning, budget management, and business performance… | business-finance-and-operations/agency-finance-tracker/SKILL.md |
| agency-financial-analyst | Expert financial analyst specializing in financial modeling, forecasting, scenario analysis, and data-driven decision… | business-finance-and-operations/agency-financial-analyst/SKILL.md |
| agency-finops-engineer | Expert cloud cost engineer for AWS/GCP/Azure — cost allocation and tagging, rightsizing, commitment planning (reserved… | business-finance-and-operations/agency-finops-engineer/SKILL.md |
| agency-fp-a-analyst | Expert Financial Planning & Analysis (FP&A) analyst specializing in budgeting, variance analysis, financial planning,… | business-finance-and-operations/agency-fp-a-analyst/SKILL.md |
| agency-m-a-integration-manager | Mergers and acquisitions integration specialist who designs and executes post-merger integration programs — covering Day 1… | business-finance-and-operations/agency-m-a-integration-manager/SKILL.md |
| agency-operations-manager | Business operations specialist who applies Lean, Six Sigma, and systems thinking to process mapping, capacity planning, KPI… | business-finance-and-operations/agency-operations-manager/SKILL.md |
| agency-report-distribution-agent | AI agent that automates distribution of consolidated sales reports to representatives based on territorial parameters | business-finance-and-operations/agency-report-distribution-agent/SKILL.md |

### commerce-sales-and-payments (15)

| Name | Function | Path under .agents/skills |
| --- | --- | --- |
| agency-china-e-commerce-operator | Expert China e-commerce operations specialist covering Taobao, Tmall, Pinduoduo, and JD ecosystems with deep expertise in… | commerce-sales-and-payments/agency-china-e-commerce-operator/SKILL.md |
| agency-cross-border-e-commerce-specialist | Full-funnel cross-border e-commerce strategist covering Amazon, Shopee, Lazada, AliExpress, Temu, and TikTok Shop… | commerce-sales-and-payments/agency-cross-border-e-commerce-specialist/SKILL.md |
| agency-korean-business-navigator | Korean business culture for foreign professionals — 품의 decision process, nunchi reading, KakaoTalk business etiquette,… | commerce-sales-and-payments/agency-korean-business-navigator/SKILL.md |
| agency-livestream-commerce-coach | Veteran livestream e-commerce coach specializing in host training and live room operations across Douyin, Kuaishou, Taobao… | commerce-sales-and-payments/agency-livestream-commerce-coach/SKILL.md |
| agency-loan-officer-assistant | Comprehensive loan officer assistant for mortgage and lending professionals — covering borrower intake, pre-qualification,… | commerce-sales-and-payments/agency-loan-officer-assistant/SKILL.md |
| agency-payments-billing-engineer | Expert payments engineer for PSP integrations (Stripe, Adyen, Braintree, PayPal), idempotent payment flows, webhook… | commerce-sales-and-payments/agency-payments-billing-engineer/SKILL.md |
| agency-pipeline-analyst | Revenue operations analyst specializing in pipeline health diagnostics, deal velocity analysis, forecast accuracy, and data-… | commerce-sales-and-payments/agency-pipeline-analyst/SKILL.md |
| agency-pricing-analyst | Specialized pricing analyst who develops optimal pricing models through market research, competitor analysis, cost structure… | commerce-sales-and-payments/agency-pricing-analyst/SKILL.md |
| agency-real-estate-buyer-seller | Comprehensive real estate agent assistant for buyer representation, seller representation, listing management, offer… | commerce-sales-and-payments/agency-real-estate-buyer-seller/SKILL.md |
| agency-retail-customer-returns | Comprehensive retail customer returns specialist for processing returns, exchanges, and refunds across in-store, online, and… | commerce-sales-and-payments/agency-retail-customer-returns/SKILL.md |
| agency-sales-coach | Expert sales coaching specialist focused on rep development, pipeline review facilitation, call coaching, deal strategy, and… | commerce-sales-and-payments/agency-sales-coach/SKILL.md |
| agency-sales-data-extraction-agent | AI agent specialized in monitoring Excel files and extracting key sales metrics (MTD, YTD, Year End) for internal live… | commerce-sales-and-payments/agency-sales-data-extraction-agent/SKILL.md |
| agency-sales-engineer | Senior pre-sales engineer specializing in technical discovery, demo engineering, POC scoping, competitive battlecards, and… | commerce-sales-and-payments/agency-sales-engineer/SKILL.md |
| agency-sales-outreach | Consultative B2B sales outreach specialist for cold prospecting, lead follow-up, objection handling, proposal writing, and… | commerce-sales-and-payments/agency-sales-outreach/SKILL.md |
| agency-salesforce-architect | Solution architecture for Salesforce platform — multi-cloud design, integration patterns, governor limits, deployment… | commerce-sales-and-payments/agency-salesforce-architect/SKILL.md |

### content-marketing-and-social (25)

| Name | Function | Path under .agents/skills |
| --- | --- | --- |
| agency-aeo-foundations-architect | Expert in AI Engine Optimization infrastructure — implements llms.txt, AI-aware robots.txt, token-budgeted content,… | content-marketing-and-social/agency-aeo-foundations-architect/SKILL.md |
| agency-app-store-optimizer | Expert app store marketing specialist focused on App Store Optimization (ASO), conversion rate optimization, and app… | content-marketing-and-social/agency-app-store-optimizer/SKILL.md |
| agency-baidu-seo-specialist | Expert Baidu search optimization specialist focused on Chinese search engine ranking, Baidu ecosystem integration, ICP… | content-marketing-and-social/agency-baidu-seo-specialist/SKILL.md |
| agency-brand-guardian | Expert brand strategist and guardian specializing in brand identity development, consistency maintenance, and strategic… | content-marketing-and-social/agency-brand-guardian/SKILL.md |
| agency-carousel-growth-engine | Autonomous TikTok and Instagram carousel generation specialist. Analyzes any website URL with Playwright, generates viral… | content-marketing-and-social/agency-carousel-growth-engine/SKILL.md |
| agency-content-creator | Expert content strategist and creator for multi-platform campaigns. Develops editorial calendars, creates compelling copy,… | content-marketing-and-social/agency-content-creator/SKILL.md |
| agency-growth-hacker | Expert growth strategist specializing in rapid user acquisition through data-driven experimentation. Develops viral loops,… | content-marketing-and-social/agency-growth-hacker/SKILL.md |
| agency-instagram-curator | Expert Instagram marketing specialist focused on visual storytelling, community building, and multi-format content… | content-marketing-and-social/agency-instagram-curator/SKILL.md |
| agency-linkedin-content-creator | Expert LinkedIn content strategist focused on thought leadership, personal brand building, and high-engagement professional… | content-marketing-and-social/agency-linkedin-content-creator/SKILL.md |
| agency-multi-platform-publisher | Expert orchestrator for one-click Chinese blog publishing. Routes a single article to 知乎 / 小红书 / CSDN / B站 / 公众号 / 掘金 via… | content-marketing-and-social/agency-multi-platform-publisher/SKILL.md |
| agency-paid-media-auditor | Comprehensive paid media auditor who systematically evaluates Google Ads, Microsoft Ads, and Meta accounts across 200+… | content-marketing-and-social/agency-paid-media-auditor/SKILL.md |
| agency-personal-growth-mentor | Cross-domain personal development mentor for goal clarity, habit design, strategic decisions, and accountability without… | content-marketing-and-social/agency-personal-growth-mentor/SKILL.md |
| agency-pr-communications-manager | Strategic public relations and communications specialist for media relations, press releases, crisis communications,… | content-marketing-and-social/agency-pr-communications-manager/SKILL.md |
| agency-private-domain-operator | Expert in building enterprise WeChat (WeCom) private domain ecosystems, with deep expertise in SCRM systems, segmented… | content-marketing-and-social/agency-private-domain-operator/SKILL.md |
| agency-programmatic-display-buyer | Display advertising and programmatic media buying specialist covering managed placements, Google Display Network, DV360,… | content-marketing-and-social/agency-programmatic-display-buyer/SKILL.md |
| agency-reddit-community-builder | Expert Reddit marketing specialist focused on authentic community engagement, value-driven content creation, and long-term… | content-marketing-and-social/agency-reddit-community-builder/SKILL.md |
| agency-seo-specialist | Expert search engine optimization strategist specializing in technical SEO, content optimization, link authority building,… | content-marketing-and-social/agency-seo-specialist/SKILL.md |
| agency-short-video-editing-coach | Hands-on short-video editing coach covering the full post-production pipeline, with mastery of CapCut Pro, Premiere Pro,… | content-marketing-and-social/agency-short-video-editing-coach/SKILL.md |
| agency-study-abroad-advisor | Full-spectrum study abroad planning expert covering the US, UK, Canada, Australia, Europe, Hong Kong, and Singapore —… | content-marketing-and-social/agency-study-abroad-advisor/SKILL.md |
| agency-tracking-measurement-specialist | Expert in conversion tracking architecture, tag management, and attribution modeling across Google Tag Manager, GA4, Google… | content-marketing-and-social/agency-tracking-measurement-specialist/SKILL.md |
| agency-twitter-engager | Expert Twitter marketing specialist focused on real-time engagement, thought leadership building, and community-driven… | content-marketing-and-social/agency-twitter-engager/SKILL.md |
| agency-video-optimization-specialist | Video marketing strategist specializing in YouTube algorithm optimization, audience retention, chaptering, thumbnail… | content-marketing-and-social/agency-video-optimization-specialist/SKILL.md |
| agency-wechat-official-account-manager | Expert WeChat Official Account (OA) strategist specializing in content marketing, subscriber engagement, and conversion… | content-marketing-and-social/agency-wechat-official-account-manager/SKILL.md |
| agency-x-twitter-intelligence-analyst | Social intelligence specialist for X/Twitter research, trend detection, account monitoring, and evidence-backed audience… | content-marketing-and-social/agency-x-twitter-intelligence-analyst/SKILL.md |
| agency-xiaohongshu-specialist | Expert Xiaohongshu marketing specialist focused on lifestyle content, trend-driven strategies, and authentic community… | content-marketing-and-social/agency-xiaohongshu-specialist/SKILL.md |

### customer-people-and-services (7)

| Name | Function | Path under .agents/skills |
| --- | --- | --- |
| agency-customer-service | Friendly, professional customer service specialist for any industry — handling inquiries, complaints, account support, FAQs,… | customer-people-and-services/agency-customer-service/SKILL.md |
| agency-customer-success-manager | Strategic customer success specialist for onboarding, health scoring, QBR facilitation, churn prevention, expansion… | customer-people-and-services/agency-customer-success-manager/SKILL.md |
| agency-hospitality-guest-services | Comprehensive hospitality guest services specialist for hotels, resorts, restaurants, and event venues — covering… | customer-people-and-services/agency-hospitality-guest-services/SKILL.md |
| agency-hr-onboarding | Comprehensive HR onboarding specialist for employee orientation, documentation management, compliance tracking, benefits… | customer-people-and-services/agency-hr-onboarding/SKILL.md |
| agency-recruitment-specialist | Expert recruitment operations and talent acquisition specialist — skilled in China's major hiring platforms, talent… | customer-people-and-services/agency-recruitment-specialist/SKILL.md |
| agency-resume-tailor | Candidate-side resume optimization specialist who analyzes job descriptions, maps real experience to role requirements,… | customer-people-and-services/agency-resume-tailor/SKILL.md |
| agency-support-responder | Expert customer support specialist delivering exceptional customer service, issue resolution, and user experience… | customer-people-and-services/agency-support-responder/SKILL.md |

### design-media-and-ux (16)

| Name | Function | Path under .agents/skills |
| --- | --- | --- |
| agency-accessibility-auditor | Expert accessibility specialist who audits interfaces against WCAG standards, tests with assistive technologies, and ensures… | design-media-and-ux/agency-accessibility-auditor/SKILL.md |
| agency-blender-add-on-engineer | Blender tooling specialist - Builds Python add-ons, asset validators, exporters, and pipeline automations that turn… | design-media-and-ux/agency-blender-add-on-engineer/SKILL.md |
| agency-image-prompt-engineer | Expert photography prompt engineer specializing in crafting detailed, evocative prompts for AI image generation. Masters the… | design-media-and-ux/agency-image-prompt-engineer/SKILL.md |
| agency-inclusive-visuals-specialist | Representation expert who defeats systemic AI biases to generate culturally accurate, affirming, and non-stereotypical… | design-media-and-ux/agency-inclusive-visuals-specialist/SKILL.md |
| agency-narrative-designer | Story systems and dialogue architect - Masters GDD-aligned narrative design, branching dialogue, lore architecture, and… | design-media-and-ux/agency-narrative-designer/SKILL.md |
| agency-persona-walkthrough-specialist | Simulate cognitive walkthroughs of web pages from a defined persona's psychological perspective — captures emotional… | design-media-and-ux/agency-persona-walkthrough-specialist/SKILL.md |
| agency-section-508-accessibility-specialist | Expert U.S. federal Section 508 accessibility engineer (the 508 legal baseline is WCAG 2.0 Level AA; WCAG 2.1/2.2 AA are… | design-media-and-ux/agency-section-508-accessibility-specialist/SKILL.md |
| agency-studio-operations | Expert operations manager specializing in day-to-day studio efficiency, process optimization, and resource coordination.… | design-media-and-ux/agency-studio-operations/SKILL.md |
| agency-studio-producer | Senior strategic leader specializing in high-level creative and technical project orchestration, resource allocation, and… | design-media-and-ux/agency-studio-producer/SKILL.md |
| agency-technical-artist | Art-to-engine pipeline specialist - Masters shaders, VFX systems, LOD pipelines, performance budgeting, and cross-engine… | design-media-and-ux/agency-technical-artist/SKILL.md |
| agency-ui-designer | Expert UI designer specializing in visual design systems, component libraries, and pixel-perfect interface creation. Creates… | design-media-and-ux/agency-ui-designer/SKILL.md |
| agency-ui-finish-gate-reviewer | Product-interface reviewer who catches generic, interchangeable UI before it ships by grounding critique in real product… | design-media-and-ux/agency-ui-finish-gate-reviewer/SKILL.md |
| agency-ux-architect | Technical architecture and UX specialist who provides developers with solid foundations, CSS systems, and clear… | design-media-and-ux/agency-ux-architect/SKILL.md |
| agency-ux-researcher | Expert user experience researcher specializing in user behavior analysis, usability testing, and data-driven design… | design-media-and-ux/agency-ux-researcher/SKILL.md |
| agency-visual-storyteller | Expert visual communication specialist focused on creating compelling visual narratives, multimedia content, and brand… | design-media-and-ux/agency-visual-storyteller/SKILL.md |
| agency-whimsy-injector | Expert creative specialist focused on adding personality, delight, and playful elements to brand experiences. Creates… | design-media-and-ux/agency-whimsy-injector/SKILL.md |

### design-system (1)

| Name | Function | Path under .agents/skills |
| --- | --- | --- |
| design-system | Token architecture, component specifications, and slide generation. Three-layer tokens (primitive→semantic→component), CSS… | design-system/SKILL.md |

### document-ocr-review (3)

| Name | Function | Path under .agents/skills |
| --- | --- | --- |
| iflytek-ocr-invoice | Use when user asks to recognize invoices, extract receipt data, or OCR bills and tickets. Recognize and extract structured… | document-ocr-review/iflytek-ocr-invoice/SKILL.md |
| iflytek-pdf-image-ocr | ifly-pdf-image-ocr skill supporting both image OCR (AI-powered LLM OCR) and PDF document recognition. Use when user asks to… | document-ocr-review/iflytek-pdf-image-ocr/SKILL.md |
| iflytek-text-proofread | iFlytek Official Document Proofreading (公文校对) — detect and correct errors in Chinese text including typos, punctuation, word… | document-ocr-review/iflytek-text-proofread/SKILL.md |

### engineering-and-technology (51)

| Name | Function | Path under .agents/skills |
| --- | --- | --- |
| agency-api-platform-engineer | Expert API platform engineer for public and partner APIs — contract-first design (OpenAPI/gRPC), versioning and deprecation… | engineering-and-technology/agency-api-platform-engineer/SKILL.md |
| agency-api-tester | Expert API testing specialist focused on comprehensive API validation, performance testing, and quality assurance across all… | engineering-and-technology/agency-api-tester/SKILL.md |
| agency-backend-architect | Senior backend architect specializing in scalable system design, database architecture, API development, and cloud… | engineering-and-technology/agency-backend-architect/SKILL.md |
| agency-cms-developer | Drupal and WordPress specialist for theme development, custom plugins/modules, content architecture, and code-first CMS… | engineering-and-technology/agency-cms-developer/SKILL.md |
| agency-code-reviewer | Expert code reviewer who provides constructive, actionable feedback focused on correctness, maintainability, security, and… | engineering-and-technology/agency-code-reviewer/SKILL.md |
| agency-codebase-onboarding-engineer | Expert developer onboarding specialist who helps new engineers understand unfamiliar codebases fast by reading source code,… | engineering-and-technology/agency-codebase-onboarding-engineer/SKILL.md |
| agency-database-optimizer | Expert database specialist focusing on schema design, query optimization, indexing strategies, and performance tuning for… | engineering-and-technology/agency-database-optimizer/SKILL.md |
| agency-database-reliability-engineer | Expert database reliability engineer (DBRE) — high availability and replication, automated failover, backup and point-in-… | engineering-and-technology/agency-database-reliability-engineer/SKILL.md |
| agency-desktop-app-engineer | Expert desktop application engineer for Electron and Tauri — secure IPC and process isolation, code signing and… | engineering-and-technology/agency-desktop-app-engineer/SKILL.md |
| agency-developer-advocate | Expert developer advocate specializing in building developer communities, creating compelling technical content, optimizing… | engineering-and-technology/agency-developer-advocate/SKILL.md |
| agency-developer-tooling-engineer | Expert developer-tooling and CLI engineer — building command-line tools and internal developer platforms with great DX:… | engineering-and-technology/agency-developer-tooling-engineer/SKILL.md |
| agency-devops-automator | Expert DevOps engineer specializing in infrastructure automation, CI/CD pipeline development, and cloud operations | engineering-and-technology/agency-devops-automator/SKILL.md |
| agency-drupal-performance-engineer | Expert Drupal 10/11 performance engineer specializing in Core Web Vitals, render and dynamic page caching, BigPipe, cache… | engineering-and-technology/agency-drupal-performance-engineer/SKILL.md |
| agency-drupal-shopping-cart-engineer | Expert Drupal e-commerce engineer specializing in Drupal Commerce for product catalog management, payment gateway… | engineering-and-technology/agency-drupal-shopping-cart-engineer/SKILL.md |
| agency-embedded-firmware-engineer | Specialist in bare-metal and RTOS firmware - ESP32/ESP-IDF, PlatformIO, Arduino, ARM Cortex-M, STM32 HAL/LL, Nordic nRF5/nRF… | engineering-and-technology/agency-embedded-firmware-engineer/SKILL.md |
| agency-expo-react-native-engineer | Production Expo and React Native implementation skill for TypeScript, Expo Router, app configuration, development builds,… | engineering-and-technology/agency-expo-react-native-engineer/SKILL.md |
| agency-feishu-integration-developer | Full-stack integration expert specializing in the Feishu (Lark) Open Platform — proficient in Feishu bots, mini programs,… | engineering-and-technology/agency-feishu-integration-developer/SKILL.md |
| agency-frontend-developer | Expert frontend developer specializing in modern web technologies, React/Vue/Angular frameworks, UI implementation, and… | engineering-and-technology/agency-frontend-developer/SKILL.md |
| agency-gaussdb-expert-engineer | Expert database specialist focusing on GaussDB OLTP — Huawei's self-developed enterprise-grade relational database (NOT… | engineering-and-technology/agency-gaussdb-expert-engineer/SKILL.md |
| agency-iot-fleet-engineer | Expert IoT and edge fleet engineer — device provisioning and identity, MQTT/telemetry pipelines, staged over-the-air (OTA)… | engineering-and-technology/agency-iot-fleet-engineer/SKILL.md |
| agency-it-service-manager | Expert IT service management specialist using ITIL 4 framework for service catalog design, incident and problem management,… | engineering-and-technology/agency-it-service-manager/SKILL.md |
| agency-lsp-index-engineer | Language Server Protocol specialist building unified code intelligence systems through LSP client orchestration and semantic… | engineering-and-technology/agency-lsp-index-engineer/SKILL.md |
| agency-mcp-builder | Expert Model Context Protocol developer who designs, builds, and tests MCP servers that extend AI agent capabilities with… | engineering-and-technology/agency-mcp-builder/SKILL.md |
| agency-minimal-change-engineer | Engineering specialist focused on minimum-viable diffs — fixes only what was asked, refuses scope creep, prefers three… | engineering-and-technology/agency-minimal-change-engineer/SKILL.md |
| agency-mobile-app-builder | Production-oriented mobile application orchestrator for React Native, Expo, Expo Router, TypeScript, native Android/iOS,… | engineering-and-technology/agency-mobile-app-builder/SKILL.md |
| agency-mobile-audio-speech-engineer | Mobile TTS, speech recognition, pronunciation, recording, microphone permission, audio session, Web fallback, and optional… | engineering-and-technology/agency-mobile-audio-speech-engineer/SKILL.md |
| agency-mobile-device-qa | Real-device quality assurance for Android, iOS, Expo development builds, release artifacts, and Web fallback. Covers… | engineering-and-technology/agency-mobile-device-qa/SKILL.md |
| agency-mobile-offline-data-engineer | Offline-first mobile data engineering for Expo SQLite and React Native, covering schema design, indexes, transactions,… | engineering-and-technology/agency-mobile-offline-data-engineer/SKILL.md |
| agency-mobile-release-engineer | Expert mobile release and distribution engineer for iOS and Android — code signing, provisioning, fastlane pipelines, App… | engineering-and-technology/agency-mobile-release-engineer/SKILL.md |
| agency-mobile-testing-engineer | Production mobile testing strategy for React Native and Expo covering unit, component, integration, navigation, SQLite,… | engineering-and-technology/agency-mobile-testing-engineer/SKILL.md |
| agency-mobile-ui-ux-engineer | Mobile-first UI and UX engineering for React Native and Expo, including safe areas, touch targets, keyboard avoidance,… | engineering-and-technology/agency-mobile-ui-ux-engineer/SKILL.md |
| agency-network-engineer | Expert network engineer for Cisco IOS/IOS-XE, Cisco ASA/FTD, Juniper Junos, and Palo Alto PAN-OS routing, switching,… | engineering-and-technology/agency-network-engineer/SKILL.md |
| agency-orgscript-engineer | Expert in designing, parsing, and implementing OrgScript grammar, AST validation, and business logic definitions. | engineering-and-technology/agency-orgscript-engineer/SKILL.md |
| agency-rapid-prototyper | Specialized in ultra-fast proof-of-concept development and MVP creation using efficient tools and frameworks | engineering-and-technology/agency-rapid-prototyper/SKILL.md |
| agency-react-native-native-modules-engineer | Safe React Native and Expo native integration for Kotlin, Swift, Android/iOS permissions, Expo Modules API, Turbo Native… | engineering-and-technology/agency-react-native-native-modules-engineer/SKILL.md |
| agency-react-native-performance-engineer | Evidence-driven React Native and Expo performance engineering for startup, TTI, JS/UI thread work, lists, SQLite queries,… | engineering-and-technology/agency-react-native-performance-engineer/SKILL.md |
| agency-realtime-collaboration-engineer | Expert realtime systems engineer for WebSocket/SSE infrastructure, presence, CRDT and OT-based collaborative editing,… | engineering-and-technology/agency-realtime-collaboration-engineer/SKILL.md |
| agency-rust-refactoring-specialist | Expert Rust engineer for repository-scale refactoring, safe renames, module restructuring, duplication removal, panic… | engineering-and-technology/agency-rust-refactoring-specialist/SKILL.md |
| agency-senior-developer | Premium implementation specialist - Masters Laravel/Livewire/FluxUI, advanced CSS, Three.js integration | engineering-and-technology/agency-senior-developer/SKILL.md |
| agency-software-architect | Expert software architect specializing in system design, domain-driven design, architectural patterns, and technical… | engineering-and-technology/agency-software-architect/SKILL.md |
| agency-solidity-smart-contract-engineer | Expert Solidity developer specializing in EVM smart contract architecture, gas optimization, upgradeable proxy patterns,… | engineering-and-technology/agency-solidity-smart-contract-engineer/SKILL.md |
| agency-solution-engineer | Hands-on GIS prototype builder who takes strategy from Technical Consultant and turns it into working demos, proof-of-… | engineering-and-technology/agency-solution-engineer/SKILL.md |
| agency-sre-site-reliability-engineer | Expert site reliability engineer specializing in SLOs, error budgets, observability, chaos engineering, and toil reduction… | engineering-and-technology/agency-sre-site-reliability-engineer/SKILL.md |
| agency-terminal-integration-specialist | Terminal emulation, text rendering optimization, and SwiftTerm integration for modern Swift applications | engineering-and-technology/agency-terminal-integration-specialist/SKILL.md |
| agency-test-results-analyzer | Expert test analysis specialist focused on comprehensive test result evaluation, quality metrics analysis, and actionable… | engineering-and-technology/agency-test-results-analyzer/SKILL.md |
| agency-tool-evaluator | Expert technology assessment specialist focused on evaluating, testing, and recommending tools, software, and platforms for… | engineering-and-technology/agency-tool-evaluator/SKILL.md |
| agency-video-streaming-engineer | Expert video streaming engineer for adaptive bitrate delivery — HLS/DASH packaging, ffmpeg transcode ladders, CMAF low-… | engineering-and-technology/agency-video-streaming-engineer/SKILL.md |
| agency-webassembly-engineer | Expert WebAssembly engineer — compiling Rust/C++/Go to Wasm, JS interop and the boundary marshalling cost, WASI and server-… | engineering-and-technology/agency-webassembly-engineer/SKILL.md |
| agency-wechat-mini-program-developer | Expert WeChat Mini Program developer specializing in 小程序 development with WXML/WXSS/WXS, WeChat API integration, payment… | engineering-and-technology/agency-wechat-mini-program-developer/SKILL.md |
| agency-wordpress-performance-engineer | Expert WordPress performance engineer specializing in Core Web Vitals, object caching (Redis/Memcached), page caching,… | engineering-and-technology/agency-wordpress-performance-engineer/SKILL.md |
| agency-wordpress-shopping-cart-engineer | Expert WordPress e-commerce engineer specializing in WooCommerce for product catalog management, payment gateway… | engineering-and-technology/agency-wordpress-shopping-cart-engineer/SKILL.md |

### games-immersive-and-3d (24)

| Name | Function | Path under .agents/skills |
| --- | --- | --- |
| agency-3d-scene-developer | Web 3D visualization specialist who creates immersive 3D scenes, terrain models, point cloud visualizations, and interactive… | games-immersive-and-3d/agency-3d-scene-developer/SKILL.md |
| agency-filament-optimization-specialist | Expert in restructuring and optimizing Filament PHP admin interfaces for maximum usability and efficiency. Focuses on… | games-immersive-and-3d/agency-filament-optimization-specialist/SKILL.md |
| agency-game-audio-engineer | Interactive audio specialist - Masters FMOD/Wwise integration, adaptive music systems, spatial audio, and audio performance… | games-immersive-and-3d/agency-game-audio-engineer/SKILL.md |
| agency-game-designer | Systems and mechanics architect - Masters GDD authorship, player psychology, economy balancing, and gameplay loop design… | games-immersive-and-3d/agency-game-designer/SKILL.md |
| agency-godot-gameplay-scripter | Composition and signal integrity specialist - Masters GDScript 2.0, C# integration, node-based architecture, and type-safe… | games-immersive-and-3d/agency-godot-gameplay-scripter/SKILL.md |
| agency-godot-multiplayer-engineer | Godot 4 networking specialist - Masters the MultiplayerAPI, scene replication, ENet/WebRTC transport, RPCs, and authority… | games-immersive-and-3d/agency-godot-multiplayer-engineer/SKILL.md |
| agency-godot-shader-developer | Godot 4 visual effects specialist - Masters the Godot Shading Language (GLSL-like), VisualShader editor, CanvasItem and… | games-immersive-and-3d/agency-godot-shader-developer/SKILL.md |
| agency-level-designer | Spatial storytelling and flow specialist - Masters layout theory, pacing architecture, encounter design, and environmental… | games-immersive-and-3d/agency-level-designer/SKILL.md |
| agency-macos-spatial-metal-engineer | Native Swift and Metal specialist building high-performance 3D rendering systems and spatial computing experiences for macOS… | games-immersive-and-3d/agency-macos-spatial-metal-engineer/SKILL.md |
| agency-roblox-avatar-creator | Roblox UGC and avatar pipeline specialist - Masters Roblox's avatar system, UGC item creation, accessory rigging, texture… | games-immersive-and-3d/agency-roblox-avatar-creator/SKILL.md |
| agency-roblox-experience-designer | Roblox platform UX and monetization specialist - Masters engagement loop design, DataStore-driven progression, Roblox… | games-immersive-and-3d/agency-roblox-experience-designer/SKILL.md |
| agency-roblox-systems-scripter | Roblox platform engineering specialist - Masters Luau, the client-server security model, RemoteEvents/RemoteFunctions,… | games-immersive-and-3d/agency-roblox-systems-scripter/SKILL.md |
| agency-unity-architect | Data-driven modularity specialist - Masters ScriptableObjects, decoupled systems, and single-responsibility component design… | games-immersive-and-3d/agency-unity-architect/SKILL.md |
| agency-unity-editor-tool-developer | Unity editor automation specialist - Masters custom EditorWindows, PropertyDrawers, AssetPostprocessors, ScriptedImporters,… | games-immersive-and-3d/agency-unity-editor-tool-developer/SKILL.md |
| agency-unity-multiplayer-engineer | Networked gameplay specialist - Masters Netcode for GameObjects, Unity Gaming Services (Relay/Lobby), client-server… | games-immersive-and-3d/agency-unity-multiplayer-engineer/SKILL.md |
| agency-unity-shader-graph-artist | Visual effects and material specialist - Masters Unity Shader Graph, HLSL, URP/HDRP rendering pipelines, and custom pass… | games-immersive-and-3d/agency-unity-shader-graph-artist/SKILL.md |
| agency-unreal-multiplayer-architect | Unreal Engine networking specialist - Masters Actor replication, GameMode/GameState architecture, server-authoritative… | games-immersive-and-3d/agency-unreal-multiplayer-architect/SKILL.md |
| agency-unreal-systems-engineer | Performance and hybrid architecture specialist - Masters C++/Blueprint continuum, Nanite geometry, Lumen GI, and Gameplay… | games-immersive-and-3d/agency-unreal-systems-engineer/SKILL.md |
| agency-unreal-technical-artist | Unreal Engine visual pipeline specialist - Masters the Material Editor, Niagara VFX, Procedural Content Generation, and the… | games-immersive-and-3d/agency-unreal-technical-artist/SKILL.md |
| agency-unreal-world-builder | Open-world and environment specialist - Masters UE5 World Partition, Landscape, procedural foliage, HLOD, and large-scale… | games-immersive-and-3d/agency-unreal-world-builder/SKILL.md |
| agency-visionos-spatial-engineer | Native visionOS spatial computing, SwiftUI volumetric interfaces, and Liquid Glass design implementation | games-immersive-and-3d/agency-visionos-spatial-engineer/SKILL.md |
| agency-xr-cockpit-interaction-specialist | Specialist in designing and developing immersive cockpit-based control systems for XR environments | games-immersive-and-3d/agency-xr-cockpit-interaction-specialist/SKILL.md |
| agency-xr-immersive-developer | Expert WebXR and immersive technology developer with specialization in browser-based AR/VR/XR applications | games-immersive-and-3d/agency-xr-immersive-developer/SKILL.md |
| agency-xr-interface-architect | Spatial interaction designer and interface strategist for immersive AR/VR/XR environments | games-immersive-and-3d/agency-xr-interface-architect/SKILL.md |

### geospatial-built-environment (42)

| Name | Function | Path under .agents/skills |
| --- | --- | --- |
| agency-account-strategist | Expert post-sale account strategist specializing in land-and-expand execution, stakeholder mapping, QBR facilitation, and… | geospatial-built-environment/agency-account-strategist/SKILL.md |
| agency-ad-creative-strategist | Paid media creative specialist focused on ad copywriting, RSA optimization, asset group design, and creative testing… | geospatial-built-environment/agency-ad-creative-strategist/SKILL.md |
| agency-ai-citation-strategist | Expert in AI recommendation engine optimization (AEO/GEO) — audits brand visibility across ChatGPT, Claude, Gemini, and… | geospatial-built-environment/agency-ai-citation-strategist/SKILL.md |
| agency-anthropologist | Expert in cultural systems, rituals, kinship, belief systems, and ethnographic method — builds culturally coherent societies… | geospatial-built-environment/agency-anthropologist/SKILL.md |
| agency-bilibili-content-strategist | Expert Bilibili marketing specialist focused on UP主 growth, danmaku culture mastery, B站 algorithm optimization, community… | geospatial-built-environment/agency-bilibili-content-strategist/SKILL.md |
| agency-bim-gis-specialist | Integration specialist who bridges Building Information Modeling and Geographic Information Systems — Revit/IFC data… | geospatial-built-environment/agency-bim-gis-specialist/SKILL.md |
| agency-business-strategist | Senior management consulting specialist for competitive analysis, market entry strategy, business model design, growth… | geospatial-built-environment/agency-business-strategist/SKILL.md |
| agency-cartography-designer | Map aesthetics specialist who designs beautiful, readable, and effective maps — color theory, typography, label placement,… | geospatial-built-environment/agency-cartography-designer/SKILL.md |
| agency-china-market-localization-strategist | Full-stack China market localization expert who transforms real-time trend signals into executable go-to-market strategies… | geospatial-built-environment/agency-china-market-localization-strategist/SKILL.md |
| agency-civil-engineer | Expert civil and structural engineer with global standards coverage — Eurocode, DIN, ACI, AISC, ASCE, AS/NZS, CSA, GB, IS,… | geospatial-built-environment/agency-civil-engineer/SKILL.md |
| agency-codebase-archaeologist | Multi-session, multi-tool drift detection specialist who audits codebases touched by several AI coding tools (Claude,… | geospatial-built-environment/agency-codebase-archaeologist/SKILL.md |
| agency-cultural-intelligence-strategist | CQ specialist that detects invisible exclusion, researches global context, and ensures software resonates authentically… | geospatial-built-environment/agency-cultural-intelligence-strategist/SKILL.md |
| agency-deal-strategist | Senior deal strategist specializing in MEDDPICC qualification, competitive positioning, and win planning for complex B2B… | geospatial-built-environment/agency-deal-strategist/SKILL.md |
| agency-douyin-strategist | Short-video marketing expert specializing in the Douyin platform, with deep expertise in recommendation algorithm mechanics,… | geospatial-built-environment/agency-douyin-strategist/SKILL.md |
| agency-drone-reality-mapping-specialist | Photogrammetry and reality capture expert who processes drone imagery into orthomosaics, digital terrain models, point… | geospatial-built-environment/agency-drone-reality-mapping-specialist/SKILL.md |
| agency-email-marketing-strategist | Expert email marketing strategist for CRM-driven campaigns, lifecycle automation, segmentation architecture, and… | geospatial-built-environment/agency-email-marketing-strategist/SKILL.md |
| agency-geoai-ml-engineer | Geospatial machine learning specialist who builds models for feature extraction, object detection, image segmentation, and… | geospatial-built-environment/agency-geoai-ml-engineer/SKILL.md |
| agency-geographer | Expert in physical and human geography, climate systems, cartography, and spatial analysis — builds geographically coherent… | geospatial-built-environment/agency-geographer/SKILL.md |
| agency-geoprocessing-specialist | ArcPy and Python toolbox expert who automates spatial workflows — builds .pyt toolboxes, Model Builder processes, batch… | geospatial-built-environment/agency-geoprocessing-specialist/SKILL.md |
| agency-gis-analyst | Day-to-day GIS operator who creates maps, manages layers, performs spatial queries, and maintains geospatial data integrity… | geospatial-built-environment/agency-gis-analyst/SKILL.md |
| agency-gis-qa-engineer | Quality assurance specialist who validates geospatial data integrity — topology checks, metadata audits, CRS consistency,… | geospatial-built-environment/agency-gis-qa-engineer/SKILL.md |
| agency-global-podcast-strategist | Expert podcast growth specialist focused on show positioning, audience development, content strategy, and monetisation.… | geospatial-built-environment/agency-global-podcast-strategist/SKILL.md |
| agency-healthcare-innovation-strategist | Strategic narrative architect for healthcare founders operating at | geospatial-built-environment/agency-healthcare-innovation-strategist/SKILL.md |
| agency-kuaishou-strategist | Expert Kuaishou marketing strategist specializing in short-video content for China's lower-tier city markets, live commerce… | geospatial-built-environment/agency-kuaishou-strategist/SKILL.md |
| agency-narratologist | Expert in narrative theory, story structure, character arcs, and literary analysis — grounds advice in established… | geospatial-built-environment/agency-narratologist/SKILL.md |
| agency-offer-lead-gen-strategist | Top-of-funnel architect who designs irresistible offers and lead magnets that attract qualified buyers at scale. Specializes… | geospatial-built-environment/agency-offer-lead-gen-strategist/SKILL.md |
| agency-organizational-psychologist | Applied organizational psychologist who diagnoses team dynamics, psychological safety, burnout risk, and culture health —… | geospatial-built-environment/agency-organizational-psychologist/SKILL.md |
| agency-outbound-strategist | Signal-based outbound specialist who designs multi-channel prospecting sequences, defines ICPs, and builds pipeline through… | geospatial-built-environment/agency-outbound-strategist/SKILL.md |
| agency-paid-social-strategist | Cross-platform paid social advertising specialist covering Meta (Facebook/Instagram), LinkedIn, TikTok, Pinterest, X, and… | geospatial-built-environment/agency-paid-social-strategist/SKILL.md |
| agency-podcast-strategist | Content strategy and operations expert for the Chinese podcast market, with deep expertise in Xiaoyuzhou, Ximalaya, and… | geospatial-built-environment/agency-podcast-strategist/SKILL.md |
| agency-ppc-campaign-strategist | Senior paid media strategist specializing in large-scale search, shopping, and performance max campaign architecture across… | geospatial-built-environment/agency-ppc-campaign-strategist/SKILL.md |
| agency-proposal-strategist | Strategic proposal architect who transforms RFPs and sales opportunities into compelling win narratives. Specializes in win… | geospatial-built-environment/agency-proposal-strategist/SKILL.md |
| agency-psychologist | Expert in human behavior, personality theory, motivation, and cognitive patterns — builds psychologically credible… | geospatial-built-environment/agency-psychologist/SKILL.md |
| agency-social-media-strategist | Expert social media strategist for LinkedIn, Twitter, and professional platforms. Creates cross-platform campaigns, builds… | geospatial-built-environment/agency-social-media-strategist/SKILL.md |
| agency-spatial-data-engineer | ETL specialist who transforms messy geospatial data from any source into clean, standardized, production-ready datasets —… | geospatial-built-environment/agency-spatial-data-engineer/SKILL.md |
| agency-spatial-data-scientist | Advanced spatial analytics specialist who applies statistical modeling, spatial econometrics, clustering, and predictive… | geospatial-built-environment/agency-spatial-data-scientist/SKILL.md |
| agency-supply-chain-strategist | Expert supply chain management and procurement strategy specialist — skilled in supplier development, strategic sourcing,… | geospatial-built-environment/agency-supply-chain-strategist/SKILL.md |
| agency-tax-strategist | Expert tax strategist specializing in tax optimization, multi-jurisdictional compliance, transfer pricing, and strategic tax… | geospatial-built-environment/agency-tax-strategist/SKILL.md |
| agency-tiktok-strategist | Expert TikTok marketing specialist focused on viral content creation, algorithm optimization, and community building.… | geospatial-built-environment/agency-tiktok-strategist/SKILL.md |
| agency-web-gis-developer | Full-stack web GIS engineer who builds interactive mapping applications — MapLibre GL JS, ArcGIS JS API, Leaflet, real-time… | geospatial-built-environment/agency-web-gis-developer/SKILL.md |
| agency-weibo-strategist | Full-spectrum operations expert for Sina Weibo, with deep expertise in trending topic mechanics, Super Topic community… | geospatial-built-environment/agency-weibo-strategist/SKILL.md |
| agency-zhihu-strategist | Expert Zhihu marketing specialist focused on thought leadership, community credibility, and knowledge-driven engagement.… | geospatial-built-environment/agency-zhihu-strategist/SKILL.md |

### government-and-public-sector (4)

| Name | Function | Path under .agents/skills |
| --- | --- | --- |
| agency-esg-sustainability-officer | Corporate sustainability strategist and ESG reporting specialist who builds environmental, social, and governance programs,… | government-and-public-sector/agency-esg-sustainability-officer/SKILL.md |
| agency-government-digital-presales-consultant | Presales expert for China's government digital transformation market (ToG), proficient in policy interpretation, solution… | government-and-public-sector/agency-government-digital-presales-consultant/SKILL.md |
| agency-grant-writer | Expert grant writing specialist for nonprofits, research institutions, and social enterprises — covering prospect research,… | government-and-public-sector/agency-grant-writer/SKILL.md |
| agency-uswds-developer | Expert U.S. Web Design System frontend developer specializing in USWDS components and design tokens, accessible-by-default… | government-and-public-sector/agency-uswds-developer/SKILL.md |

### healthcare-and-life-sciences (5)

| Name | Function | Path under .agents/skills |
| --- | --- | --- |
| agency-aging-parent-care-companion | Compassionate, HIPAA-aligned care coordination and decision-support agent for family caregivers managing an aging parent's… | healthcare-and-life-sciences/agency-aging-parent-care-companion/SKILL.md |
| agency-clinical-evidence-agent | Evidence standards and clinical credibility framework for AI agents | healthcare-and-life-sciences/agency-clinical-evidence-agent/SKILL.md |
| agency-healthcare-customer-service | Empathetic healthcare customer service specialist for patient support, billing inquiries, appointment management, insurance… | healthcare-and-life-sciences/agency-healthcare-customer-service/SKILL.md |
| agency-medical-billing-coding-specialist | Expert medical billing and coding specialist for ICD-10-CM/PCS, CPT, and HCPCS coding, claim submission, denial management,… | healthcare-and-life-sciences/agency-medical-billing-coding-specialist/SKILL.md |
| agency-sovereign-health-systems-agent | Government health mandate engagement framework for AI agents | healthcare-and-life-sciences/agency-sovereign-health-systems-agent/SKILL.md |

### legal-security-and-compliance (23)

| Name | Function | Path under .agents/skills |
| --- | --- | --- |
| agency-agentic-identity-trust-architect | Designs identity, authentication, and trust verification systems for autonomous AI agents operating in multi-agent… | legal-security-and-compliance/agency-agentic-identity-trust-architect/SKILL.md |
| agency-ai-generated-code-security-auditor | Security reviewer for AI-generated and vibe-coded apps — hunts the hardcoded secrets, broken row-level security, and prompt-… | legal-security-and-compliance/agency-ai-generated-code-security-auditor/SKILL.md |
| agency-application-security-engineer | AppSec specialist who secures the software development lifecycle through threat modeling, secure code review, SAST/DAST… | legal-security-and-compliance/agency-application-security-engineer/SKILL.md |
| agency-blockchain-security-auditor | Expert smart contract security auditor specializing in vulnerability detection, formal verification, exploit analysis, and… | legal-security-and-compliance/agency-blockchain-security-auditor/SKILL.md |
| agency-cloud-security-architect | Cloud-native security specialist designing zero trust architectures, implementing defense-in-depth across AWS, Azure, and… | legal-security-and-compliance/agency-cloud-security-architect/SKILL.md |
| agency-compliance-auditor | Expert technical compliance auditor specializing in SOC 2, ISO 27001, HIPAA, and PCI-DSS audits — from readiness assessment… | legal-security-and-compliance/agency-compliance-auditor/SKILL.md |
| agency-data-privacy-officer | Corporate data privacy specialist and DPO who builds GDPR, CCPA, and global privacy compliance programs — covering data… | legal-security-and-compliance/agency-data-privacy-officer/SKILL.md |
| agency-fedramp-rmf-compliance-engineer | Expert FedRAMP and NIST Risk Management Framework compliance engineer specializing in both FedRAMP authorization pathways —… | legal-security-and-compliance/agency-fedramp-rmf-compliance-engineer/SKILL.md |
| agency-healthcare-marketing-compliance-specialist | Expert in healthcare marketing compliance in China, proficient in the Advertising Law, Medical Advertisement Management… | legal-security-and-compliance/agency-healthcare-marketing-compliance-specialist/SKILL.md |
| agency-identity-access-engineer | Expert identity engineer for OAuth 2.0/OIDC flows, enterprise SSO (SAML/OIDC) and SCIM provisioning, passkeys/WebAuthn,… | legal-security-and-compliance/agency-identity-access-engineer/SKILL.md |
| agency-identity-graph-operator | Operates a shared identity graph that multiple AI agents resolve against. Ensures every agent in a multi-agent system gets… | legal-security-and-compliance/agency-identity-graph-operator/SKILL.md |
| agency-incident-responder | Digital forensics and incident response specialist who leads breach investigations, contains active threats, coordinates… | legal-security-and-compliance/agency-incident-responder/SKILL.md |
| agency-incident-response-commander | Expert incident commander specializing in production incident management, structured response coordination, post-mortem… | legal-security-and-compliance/agency-incident-response-commander/SKILL.md |
| agency-legal-billing-time-tracking | Comprehensive legal billing and time tracking specialist for accurate time capture, invoice generation, billing narrative… | legal-security-and-compliance/agency-legal-billing-time-tracking/SKILL.md |
| agency-legal-client-intake | Comprehensive legal client intake specialist for qualifying prospects, collecting case information, scheduling… | legal-security-and-compliance/agency-legal-client-intake/SKILL.md |
| agency-legal-compliance-checker | Expert legal and compliance specialist ensuring business operations, data handling, and content creation comply with… | legal-security-and-compliance/agency-legal-compliance-checker/SKILL.md |
| agency-legal-document-review | Comprehensive legal document review specialist for contracts, litigation documents, and real estate agreements — summarizing… | legal-security-and-compliance/agency-legal-document-review/SKILL.md |
| agency-privacy-engineer | Expert privacy engineer who implements privacy in code — PII discovery and classification, data minimization, consent… | legal-security-and-compliance/agency-privacy-engineer/SKILL.md |
| agency-secrets-credential-hygiene-engineer | Owns the full lifecycle of secrets and credentials — detection, prevention, vaulting, rotation, and leak response — so an… | legal-security-and-compliance/agency-secrets-credential-hygiene-engineer/SKILL.md |
| agency-security-architect | Expert security architect specializing in threat modeling, secure-by-design architecture, trust-boundary analysis, defense-… | legal-security-and-compliance/agency-security-architect/SKILL.md |
| agency-senior-secops-engineer | Defensive application security specialist who scans every code submission for secrets and sensitive data exposure before… | legal-security-and-compliance/agency-senior-secops-engineer/SKILL.md |
| agency-threat-intelligence-analyst | Cyber threat intelligence specialist who tracks adversary groups, maps attack campaigns to MITRE ATT&CK, produces actionable… | legal-security-and-compliance/agency-threat-intelligence-analyst/SKILL.md |
| agency-zk-steward | Knowledge-base steward in the spirit of Niklas Luhmann's Zettelkasten. Default perspective: Luhmann; switches to domain… | legal-security-and-compliance/agency-zk-steward/SKILL.md |

### product-project-and-strategy (16)

| Name | Function | Path under .agents/skills |
| --- | --- | --- |
| agency-behavioral-nudge-engine | Behavioral psychology specialist that adapts software interaction cadences and styles to maximize user motivation and… | product-project-and-strategy/agency-behavioral-nudge-engine/SKILL.md |
| agency-change-management-consultant | Expert change management specialist using ADKAR, Kotter, and Prosci frameworks to guide organizations through technology… | product-project-and-strategy/agency-change-management-consultant/SKILL.md |
| agency-chief-of-staff | Master coordinator for founders and executives — filters noise, owns processes, enforces consistency, routes decisions, and… | product-project-and-strategy/agency-chief-of-staff/SKILL.md |
| agency-discovery-coach | Coaches sales teams on elite discovery methodology — question design, current-state mapping, gap quantification, and call… | product-project-and-strategy/agency-discovery-coach/SKILL.md |
| agency-executive-summary-generator | Consultant-grade AI specialist trained to think and communicate like a senior strategy consultant. Transforms complex… | product-project-and-strategy/agency-executive-summary-generator/SKILL.md |
| agency-experiment-tracker | Expert project manager specializing in experiment design, execution tracking, and data-driven decision making. Focused on… | product-project-and-strategy/agency-experiment-tracker/SKILL.md |
| agency-git-workflow-master | Expert in Git workflows, branching strategies, and version control best practices including conventional commits, rebasing,… | product-project-and-strategy/agency-git-workflow-master/SKILL.md |
| agency-jira-workflow-steward | Expert delivery operations specialist who enforces Jira-linked Git workflows, traceable commits, structured pull requests,… | product-project-and-strategy/agency-jira-workflow-steward/SKILL.md |
| agency-product-manager | Holistic product leader who owns the full product lifecycle — from discovery and strategy through roadmap, stakeholder… | product-project-and-strategy/agency-product-manager/SKILL.md |
| agency-project-shepherd | Expert project manager specializing in cross-functional project coordination, timeline management, and stakeholder… | product-project-and-strategy/agency-project-shepherd/SKILL.md |
| agency-senior-project-manager | Converts specs to tasks and remembers previous projects. Focused on realistic scope, no background processes, exact spec… | product-project-and-strategy/agency-senior-project-manager/SKILL.md |
| agency-sprint-prioritizer | Expert product manager specializing in agile sprint planning, feature prioritization, and resource allocation. Focused on… | product-project-and-strategy/agency-sprint-prioritizer/SKILL.md |
| agency-strategy-duel-agent | Conducts live strategy duels using game theory and the 36 Chinese stratagems | product-project-and-strategy/agency-strategy-duel-agent/SKILL.md |
| agency-technical-consultant | Strategic GIS advisor who translates business problems into geospatial solutions — gap analysis, technology roadmaps, RFP… | product-project-and-strategy/agency-technical-consultant/SKILL.md |
| agency-workflow-architect | Workflow design specialist who maps complete workflow trees for every system, user journey, and agent interaction — covering… | product-project-and-strategy/agency-workflow-architect/SKILL.md |
| agency-workflow-optimizer | Expert process improvement specialist focused on analyzing, optimizing, and automating workflows across all business… | product-project-and-strategy/agency-workflow-optimizer/SKILL.md |

### realtime-video-translation (1)

| Name | Function | Path under .agents/skills |
| --- | --- | --- |
| realtime-video-translation | Build, extend, test, or troubleshoot realtime video speech translation with live Vietnamese subtitles and optional… | realtime-video-translation/SKILL.md |

### research-and-analysis (9)

| Name | Function | Path under .agents/skills |
| --- | --- | --- |
| agency-evidence-collector | Screenshot-obsessed, fantasy-allergic QA specialist - Default to finding 3-5 issues, requires visual proof for everything | research-and-analysis/agency-evidence-collector/SKILL.md |
| agency-feedback-synthesizer | Expert in collecting, analyzing, and synthesizing user feedback from multiple channels to extract actionable product… | research-and-analysis/agency-feedback-synthesizer/SKILL.md |
| agency-french-consulting-market-navigator | Navigate the French ESN/SI freelance ecosystem — margin models, platform mechanics (Malt, collective.work), portage… | research-and-analysis/agency-french-consulting-market-navigator/SKILL.md |
| agency-historian | Expert in historical analysis, periodization, material culture, and historiography — validates historical coherence and… | research-and-analysis/agency-historian/SKILL.md |
| agency-investment-researcher | Expert investment researcher specializing in market research, due diligence, portfolio analysis, and asset valuation.… | research-and-analysis/agency-investment-researcher/SKILL.md |
| agency-performance-benchmarker | Expert performance testing and optimization specialist focused on measuring, analyzing, and improving system performance… | research-and-analysis/agency-performance-benchmarker/SKILL.md |
| agency-reality-checker | Stops fantasy approvals, evidence-based certification - Default to "NEEDS WORK", requires overwhelming proof for production… | research-and-analysis/agency-reality-checker/SKILL.md |
| agency-statistician | Expert in quantitative research methodology, experimental design, and statistical inference — pressure-tests claims, designs… | research-and-analysis/agency-statistician/SKILL.md |
| agency-trend-researcher | Expert market intelligence analyst specializing in identifying emerging trends, competitive analysis, and opportunity… | research-and-analysis/agency-trend-researcher/SKILL.md |

### slides (1)

| Name | Function | Path under .agents/skills |
| --- | --- | --- |
| slides | Create strategic HTML presentations with Chart.js, design tokens, responsive layouts, copywriting formulas, and contextual… | slides/SKILL.md |

### taiwanese-mandarin-contextual-translation (1)

| Name | Function | Path under .agents/skills |
| --- | --- | --- |
| taiwanese-mandarin-contextual-translation | Translate and localize video dialogue, captions, scripts, and conversational text into natural Traditional Chinese for… | taiwanese-mandarin-contextual-translation/SKILL.md |

### translation-localization (4)

| Name | Function | Path under .agents/skills |
| --- | --- | --- |
| contextweaver-translate | Operate reviewable, resumable long-form translation projects with ContextWeaver. Use when Codex needs to import or translate… | translation-localization/contextweaver-translate/SKILL.md |
| iflytek-translate | iFlytek Machine Translation (机器翻译) — translate text between Chinese, English, Japanese, Korean, French, Spanish, German,… | translation-localization/iflytek-translate/SKILL.md |
| iflytek-video-translate | Use when user asks to translate videos, dub video content, or localize videos into other languages. Translate videos with… | translation-localization/iflytek-video-translate/SKILL.md |
| lara-translation-mcp | Translate and localize text with the Lara Translate MCP server, including language detection, terminology control,… | translation-localization/lara-translation-mcp/SKILL.md |

### ui-styling (1)

| Name | Function | Path under .agents/skills |
| --- | --- | --- |
| ui-styling | Create beautiful, accessible user interfaces with shadcn/ui components (built on Radix UI + Tailwind), Tailwind CSS utility-… | ui-styling/SKILL.md |

### video-vietnamese-dubbing (1)

| Name | Function | Path under .agents/skills |
| --- | --- | --- |
| video-vietnamese-dubbing | Build or improve Vietnamese AI dubbing for video applications from already translated Vietnamese subtitles. Use when… | video-vietnamese-dubbing/SKILL.md |

### visual-multimodal (2)

| Name | Function | Path under .agents/skills |
| --- | --- | --- |
| animated-sketch-diagram | 生成"黑墨手绘涂鸦"风格的动画架构图/流程图：米色纸面、针管笔墨线、极淡水洗色块、简笔涂鸦图标、序号章、连线上的流动圆点动画、图标微动效。产出单文件自包含动画 HTML（SVG+CSS），可一键导出无缝循环… | visual-multimodal/animated-sketch-diagram/SKILL.md |
| iflytek-image-understanding | Use when user asks to analyze an image, describe image contents, or answer questions about a picture. iFlytek Image… | visual-multimodal/iflytek-image-understanding/SKILL.md |

### web-coder (1)

| Name | Function | Path under .agents/skills |
| --- | --- | --- |
| web-coder | Expert 10x engineer with comprehensive knowledge of web development, internet protocols, and web standards. Use when working… | web-coder/SKILL.md |

### writing-language-and-training (7)

| Name | Function | Path under .agents/skills |
| --- | --- | --- |
| agency-book-co-author | Strategic thought-leadership book collaborator for founders, experts, and operators turning voice notes, fragments, and… | writing-language-and-training/agency-book-co-author/SKILL.md |
| agency-corporate-training-designer | Expert in enterprise training system design and curriculum development — proficient in training needs analysis,… | writing-language-and-training/agency-corporate-training-designer/SKILL.md |
| agency-document-generator | Expert document creation specialist who generates professional PDF, PPTX, DOCX, and XLSX files using code-based approaches… | writing-language-and-training/agency-document-generator/SKILL.md |
| agency-internationalization-engineer | Expert i18n engineer for ICU MessageFormat, CLDR plural rules, RTL and bidirectional layouts, locale-aware… | writing-language-and-training/agency-internationalization-engineer/SKILL.md |
| agency-language-translator | Real-time Spanish ↔ English translation specialist with cultural context, regional dialect awareness, travel phrase… | writing-language-and-training/agency-language-translator/SKILL.md |
| agency-meeting-notes-specialist | Extract structured decisions, action items, and open questions from meeting transcripts or rough notes into a clean… | writing-language-and-training/agency-meeting-notes-specialist/SKILL.md |
| agency-technical-writer | Expert technical writer specializing in developer documentation, API references, README files, and tutorials. Transforms… | writing-language-and-training/agency-technical-writer/SKILL.md |
