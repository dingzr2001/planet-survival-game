import json
from pathlib import Path


run_dir = Path(__file__).parent
decisions_path = run_dir / "decisions.json"
decisions = json.loads(decisions_path.read_text())

official_salaries = {
    "4719126005": "$135,000-$150,000/year base",
    "41488eae-50a9-4ad3-b6e0-2fd28efb238e": "$130,000/year base (San Francisco); CA$113,000/year base (Toronto)",
    "149f368c-52d5-408f-ba26-ad888f318a00": "$150,000-$180,000/year base",
    "29bad846-de60-4be7-a222-69b97e044930": "$155,000-$160,000/year base",
    "df6eb1ee-b360-46fe-b23f-658626ec59ea": "$165,000-$170,000/year base",
    "R117617-1": "$59,200-$98,600/year base",
    "R117626-2": "$59,200-$98,600/year base",
    "R116023": "$59,200-$98,600/year base",
    "21029": "$66,500-$123,500/year base",
    "8128744": "$122,100-$134,400/year base",
    "R30695-1": "$97,500-$160,000/year base",
    "8765715002": "$110,000-$135,000/year base",
    "8765745002": "$110,000-$135,000/year base",
    "7827894003": "$100,000-$125,000/year base",
    "JR2024557": "$108,000-$178,250/year base (Level 1); $124,000-$195,500/year base (Level 2)",
    "5225186007": "$79,040-$133,016/year base",
}

not_found = {
    "JR5078",
    "R53009-1",
}

not_found_urls = {
    "http://omegahires.applytojob.com/apply/Oj9Hboc7Zk/Java-Springboot-Python-AI-FS-Developer",
    "http://omegahires.applytojob.com/apply/aDHPuh97BW/Java-Python-AI-Developer",
}

for decision in decisions:
    if not (
        decision.get("review_status") == "OPEN_OFFICIAL"
        and decision.get("eligibility") == "ELIGIBLE"
        and decision.get("match") in {"STRONG", "GOOD"}
    ):
        continue

    job_id = decision.get("job_id", "")
    job_url = decision["job_url"]
    if job_id in official_salaries:
        decision["salary_status"] = "FOUND_OFFICIAL"
        decision["salary"] = official_salaries[job_id]
        decision["salary_source"] = job_url
    elif job_id in not_found or job_url in not_found_urls:
        decision["salary_status"] = "NOT_FOUND"
        decision["salary_search_note"] = (
            "Checked the official posting and searched the exact title and job ID; "
            "no salary applicable to this exact requisition/location was found."
        )
    else:
        raise RuntimeError(f"Missing salary review for {job_id or job_url}")

decisions_path.write_text(json.dumps(decisions, ensure_ascii=False, indent=2) + "\n")
