"""SEC-01 regression checks; run only against a fresh, isolated synthetic database.

Requires DEMO_ADMIN_PASSWORD for the locally bootstrapped admin. Creates a
synthetic applicant/employee and questionnaire records; never use real data.
Example: python scripts/sec01_http_test.py --allow-synthetic-data --base http://127.0.0.1:5136
"""
import argparse
import html.parser
import json
import os
from pathlib import Path
import re
import secrets
import urllib.parse

from smoke_test import Client


class TableRows(html.parser.HTMLParser):
    def __init__(self, body):
        super().__init__()
        self.rows = []
        self.current = None
        self.feed(body.decode())

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag == "tr":
            self.current = {"text": [], "links": [], "inputs": {}}
        elif self.current is not None:
            if tag == "a":
                self.current["links"].append(attrs.get("href", ""))
            elif tag == "input" and "name" in attrs:
                self.current["inputs"][attrs["name"]] = attrs.get("value", "")

    def handle_data(self, data):
        if self.current is not None:
            self.current["text"].append(data)

    def handle_endtag(self, tag):
        if tag == "tr" and self.current is not None:
            self.current["text"] = " ".join(" ".join(self.current["text"]).split())
            self.rows.append(self.current)
            self.current = None


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base", default="http://127.0.0.1:5136")
    parser.add_argument("--allow-synthetic-data", action="store_true")
    args = parser.parse_args()
    url = urllib.parse.urlparse(args.base)
    if not args.allow_synthetic_data or url.hostname not in {"localhost", "127.0.0.1", "::1"}:
        parser.error("Requires explicit synthetic-data consent and a loopback application URL.")
    password = os.environ.get("DEMO_ADMIN_PASSWORD")
    if not password:
        parser.error("Set DEMO_ADMIN_PASSWORD for the isolated application's bootstrap admin.")
    checks = []

    def check(condition, description):
        assert condition, description
        checks.append(description)
        print("PASS:", description)

    def login(name, credential):
        client = Client(args.base)
        result = client.request("/Account/Login", {"NationalId": name, "Password": credential,
            "__RequestVerificationToken": client.token("/Account/Login")})
        assert result[0] == 200 and "/Clients/Search" in result[1], "Staff login failed"
        return client

    admin = login("admin", password)

    def records(kind):
        result = admin.request("/Admin/" + kind)
        assert result[0] == 200 and urllib.parse.urlparse(result[1]).path == "/Admin/" + kind
        singular = "Section" if kind == "Sections" else "Question"
        found = {}
        for row in TableRows(result[2]).rows:
            for link in row["links"]:
                match = re.search(r"/Admin/Edit" + singular + r"(?:/|\?id=)(\d+)", link)
                if match:
                    found[int(match.group(1))] = row["text"]
        return found

    baseline_sections, baseline_questions = records("Sections"), records("Questions")
    check(len(baseline_sections) == 5 and len(baseline_questions) == 24,
          "Fresh seeded questionnaire required: five sections and 24 questions")
    section_id = min(baseline_sections)
    run_id = secrets.token_hex(5)

    def post(client, action, fields):
        return client.request("/Admin/" + action, dict(fields,
            __RequestVerificationToken=client.token("/Admin/" + action)), follow=False)

    # Create and promote only this test's new synthetic account, then renew its cookie.
    citizen = Client(args.base)
    national_id = "00" + str(secrets.randbelow(100_000_000)).zfill(8)
    employee_password = "Synthetic!" + secrets.token_hex(12)
    registered = citizen.request("/Account/Register", {"NationalId": national_id,
        "FullNameAr": "SEC01 synthetic " + run_id, "PhoneNumber": "0000000000",
        "Password": employee_password, "ConfirmPassword": employee_password,
        "__RequestVerificationToken": citizen.token("/Account/Register")})
    assert registered[0] == 200 and "/Clients/Details" in registered[1], "Synthetic registration failed"
    citizen_token = citizen.token(registered[1])
    for action in ("CreateQuestion", "CreateSection"):
        anonymous = Client(args.base).request("/Admin/" + action, follow=False)
        check(anonymous[0] == 302 and "/Account/Login" in anonymous[3].get("Location", ""),
              "Anonymous challenge unchanged: " + action)
        denied = citizen.request("/Admin/" + action, {"__RequestVerificationToken": citizen_token}, follow=False)
        check(denied[0] == 302 and "AccessDenied" in denied[3].get("Location", ""),
              "Citizen POST denied with valid CSRF token: " + action)
    users = TableRows(admin.request("/Admin/ManageUsers")[2]).rows
    user_id = next(row["inputs"]["userId"] for row in users if national_id in row["text"])
    promoted = admin.request("/Admin/MakeEmployee", {"userId": user_id,
        "__RequestVerificationToken": admin.token("/Admin/ManageUsers")}, follow=False)
    assert promoted[0] == 302, "Synthetic employee setup failed"
    employee = login(national_id, employee_password)

    for actor_index, (role, client) in enumerate((("Admin", admin), ("Employee", employee))):
        for action in ("CreateQuestion", "CreateSection"):
            check(client.request("/Admin/" + action, {}, follow=False)[0] == 400,
                  role + " still requires CSRF: " + action)

        for nested in (False, True):
            label = f"SEC01_{run_id}_{role}_{'nested' if nested else 'normal'}"
            questions_before, sections_before = records("Questions"), records("Sections")
            fields = {"SectionId": section_id, "QuestionTextAr": label, "QuestionTextEn": label,
                "PointValue": 3, "DisplayOrder": 900 + actor_index * 10 + int(nested), "IsActive": "true"}
            if nested:
                fields.update({"Section.SectionNameAr": "INJECTED_" + label,
                    "Section.SectionNameEn": "INJECTED_" + label, "Section.Weight": 100,
                    "Section.DisplayOrder": 99, "Section.IsActive": "true"})
            result = post(client, "CreateQuestion", fields)
            check(result[0] == 302 and f"sectionId={section_id}" in result[3].get("Location", ""),
                  role + " question creation accepts allowed fields: " + label)
            questions_after = records("Questions")
            created = set(questions_after) - set(questions_before)
            check(len(created) == 1 and label in questions_after[next(iter(created))]
                  and all(questions_after.get(k) == v for k, v in questions_before.items()),
                  role + " creates exactly one intended question; existing questions preserved")
            check(records("Sections") == sections_before, role + " submitted Section graph creates no section")

            sections_before, questions_before = records("Sections"), records("Questions")
            fields = {"SectionNameAr": label, "SectionNameEn": label, "Weight": 0,
                "DisplayOrder": 100 + actor_index * 10 + int(nested), "IsActive": "true"}
            if nested:
                fields.update({"Questions[0].QuestionTextAr": "INJECTED_" + label,
                    "Questions[0].QuestionTextEn": "INJECTED_" + label,
                    "Questions[0].PointValue": 5, "Questions[0].DisplayOrder": 1})
            result = post(client, "CreateSection", fields)
            check(result[0] == 302 and result[3].get("Location") == "/Admin/Sections",
                  role + " section creation accepts allowed fields: " + label)
            sections_after = records("Sections")
            created = set(sections_after) - set(sections_before)
            check(len(created) == 1 and label in sections_after[next(iter(created))]
                  and all(sections_after.get(k) == v for k, v in sections_before.items()),
                  role + " creates exactly one intended section; existing sections preserved")
            check(records("Questions") == questions_before, role + " submitted Questions graph creates no questions")

        sections_before, questions_before = records("Sections"), records("Questions")
        for missing_id in (-1, 0, 2147483647):
            result = post(client, "CreateQuestion", {"SectionId": missing_id,
                "QuestionTextAr": "Invalid reference", "QuestionTextEn": "Invalid reference",
                "PointValue": 1, "DisplayOrder": 999, "Section.SectionNameAr": "Injected",
                "Section.SectionNameEn": "Injected", "Section.Weight": 100})
            check(result[0] == 200 and b"Please select the section" in result[2]
                  and records("Questions") == questions_before and records("Sections") == sections_before,
                  f"{role} rejects nonexistent SectionId {missing_id} without persisting a graph")
        result = post(client, "CreateSection", {"SectionNameAr": "Excess weight", "SectionNameEn": "Excess weight",
            "Weight": 1, "DisplayOrder": 200 + actor_index, "IsActive": "true"})
        check(result[0] == 200 and records("Sections") == sections_before,
              role + " cannot bypass the existing 100-percent weight limit")

    check(all(records("Sections").get(k) == v for k, v in baseline_sections.items())
          and all(records("Questions").get(k) == v for k, v in baseline_questions.items()),
          "All pre-existing section and question rows remain unchanged")
    Path(".local").mkdir(exist_ok=True)
    Path(".local/sec01-http-result.json").write_text(json.dumps({"checks": checks}, indent=2), encoding="utf-8")
    print(f"Completed {len(checks)} SEC-01 HTTP checks.")


if __name__ == "__main__":
    main()
