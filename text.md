exams each year, and the results shape university admission, bursaries, and
Over half a million South African learners sit the National Senior Certificate
careers. The Department of Basic Education publishes these outcomes in the
inconsistently formatted spreadsheets and PDFs, one per province and year,
annual NSC School Performance Report, but the data ships as dense,
making comparisons and analysis a manual chore that parents, educators, and
officials rarely have time for.

MatricDashboard fixes this by turning the official report into a searchable,
interactive dashboard with per-school pass rates, trends, provincial and
district breakdowns, quintile context, and leaderboards, exposed through a
public API. The motivation is that school performance data is public
accountability data: easy exploration helps parents choose, principals
benchmark, departments target support, and journalists investigate.




Backend (.NET 10, Clean Architecture)                            
                                                        
- C# / ASP.NET Core Web API with layered architecture (Domain,   
Application, Infrastructure, WebApi, Shared)                   
- Entity Framework Core 10 with PostgreSQL (Npgsql)              
- ASP.NET Core Identity: JWT + refresh tokens, TOTP 2FA, email   
verification, password reset                                   
- Hangfire (PostgreSQL-backed background jobs)                   
- MeiliSearch client for full-text school search                 
- MinIO / S3-compatible file storage, MailKit for email          
- Serilog (logging), OpenTelemetry (tracing/metrics),            
FluentValidation, Asp.Versioning (API versioning)              
                                                        
Frontend (Svelte 5 / SvelteKit 2)                                
                                                        
- TypeScript, Tailwind CSS 4, shadcn-svelte (bits-ui), Lucide    
icons                                                          
- SvelteKit Superforms + Zod (form validation), Paraglide (      
i18n)                                                          
- openapi-fetch / openapi-typescript (type-safe API client       
generated from the OpenAPI spec)                               
- Vite, Vitest, ESLint, Prettier, pnpm                           
                                                        
Infrastructure & DevOps                                          
                                                        
- .NET Aspire (local orchestration with OpenTelemetry            
dashboard)                                                     
- Docker / Docker Compose: PostgreSQL, MeiliSearch, MinIO,       
Mailpit, pgAdmin                                               
- GitHub Actions CI (build + format checks)                      
                                                        
Data                                                             
                                                        
- Department of Basic Education NSC School Performance Report (  
2022-2024), CSVs ingested and cleaned into the search index    
and database                                                   



    Unique Value Proposition                                         
                                                                    
    Matric results are among the most anticipated public datasets    
    in South Africa, yet the official data is only published as      
    static spreadsheets and PDFs, one file per province and year.    
    Anyone who wants to compare schools, track performance over      
    time, or filter by district or quintile has to download, clean,  
    and analyse the files by hand.                                  
                                                                    
    MatricDashboard turns that raw, unwieldy data into a single      
    searchable, filterable platform that works instantly:            
                                                                    
    - School-level trends, not just one-year snapshots - pass        
    rates, achievement counts, and bachelor passes across 2022-    
    2024, so you can see whether a school is improving or          
    declining, not just where it landed this year.                 
    - Fast full-text search across every school in the country -     
    type a school name and get its complete performance profile    
    in milliseconds.                                               
    - Context that matters - district, province, and quintile        
    filters, so a low-quintile school outperforming its peers is   
    recognised instead of being buried in a national ranking.      
    - A public API, not just a website - every number is exposed     
    through a clean, versioned API, so parents, researchers,       
    journalists, NGOs, and other developers can build on the       
    data without fighting with spreadsheets.                       
    - One honest source of truth - the official DBE report,          
    cleaned and consolidated into one dataset, so users never      
    chase files across multiple government pages.                  
                                                                    
    The value is simple: where the Department publishes raw files,   
    MatricDashboard delivers answers.                                


**Unique Value Proposition**

Matric results are among the most anticipated public datasets in South Africa, yet the official data is only published as static spreadsheets and PDFs, one file per province and year. Anyone who wants to compare schools, track performance over time, or filter by district or quintile has to download, clean, and analyse the files by hand. MatricDashboard turns that raw, unwieldy data into a single searchable, filterable platform that works instantly:

- **School-level trends, not just one-year snapshots** - pass rates, achievement counts, and bachelor passes across 2022-2024, so you can see whether a school is improving or declining, not just where it landed this year.
- **Fast full-text search across every school in the country** - type a school name and get its complete performance profile in milliseconds.
- **Context that matters** - district, province, and quintile filters, so a low-quintile school outperforming its peers is recognised instead of being buried in a national ranking.
- **A public API, not just a website** - every number is exposed through a clean, versioned API, so parents, researchers, journalists, NGOs, and other developers can build on the data without fighting with spreadsheets.
- **One honest source of truth** - the official DBE report, cleaned and consolidated into one dataset, so users never chase files across multiple government pages.

The value is simple: where the Department publishes raw files, MatricDashboard delivers answers.

**Unfair Advantage**

The hardest part of matric data is not the analysis, it is getting the data into a usable state. The official report ships as messy, inconsistent spreadsheets and PDFs, a different file per province and year. MatricDashboard has already done that unglamorous work: a cleaned, consolidated national dataset for 2022-2024, loaded into a fast search index. That data pipeline is the moat - tedious to build and easy to underestimate, so competitors rarely replicate it.

It also knows the context that makes matric data meaningful - provinces, districts, quintiles, the NSC system - and it is a public API on clean-architecture foundations, not a throwaway dashboard, so anyone can build on it.

One line: everyone can access the Department's files, but only MatricDashboard has turned them into a single, searchable, buildable dataset.

**Customer Segments**

- **Parents and learners** - the largest audience. Choosing a school, deciding where to enrol, or seeing how a child's school is performing and trending over time.
- **School principals and educators** - benchmarking their school against peers in the district, province, and quintile, and identifying what to improve.
- **Education departments and officials** - monitoring performance, spotting struggling schools early, and targeting support and interventions where they are needed most.
- **Journalists and researchers** - instant access to comparable, trend-ready data for stories, audits, and academic studies without fighting with spreadsheets.
- **NGOs and education non-profits** - identifying underperforming or overperforming schools to direct funding and programmes.
- **Developers and data consumers** - the public API serves third-party apps, tools, and integrations that want matric data without building the pipeline themselves.
