"""Local-only HTTP regression walkthrough. Creates synthetic records; never use real data.

Run against a fresh local development database:
  python scripts/smoke_test.py --allow-synthetic-data
Optional DEMO_ADMIN_PASSWORD enables staff route/download checks.
Uses only Python's standard library; it does not bypass authentication or CSRF.
"""
import argparse
import html.parser
import http.cookiejar
import json
import os
from pathlib import Path
import re
import secrets
import urllib.error
import urllib.parse
import urllib.request


class Inputs(html.parser.HTMLParser):
    def __init__(self, text):
        super().__init__()
        self.values = {}
        self.feed(text)

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag == "input" and "name" in attrs and attrs.get("type") != "radio":
            self.values[attrs["name"]] = attrs.get("value", "")


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs):
        return None


class Client:
    def __init__(self, base):
        self.base = base
        cookies = http.cookiejar.CookieJar()
        self.normal = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cookies))
        self.raw = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cookies), NoRedirect())

    def request(self, path, data=None, *, follow=True, file=None):
        headers = {"Accept-Language": "en-US"}
        payload = None
        if file:
            boundary = "portfolio-" + secrets.token_hex(12)
            parts = []
            for key, value in data.items():
                parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{key}"\r\n\r\n{value}\r\n'.encode())
            field, name, content = file
            parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{field}"; filename="{name}"\r\nContent-Type: application/pdf\r\n\r\n'.encode() + content + b"\r\n")
            parts.append(f'--{boundary}--\r\n'.encode())
            payload = b"".join(parts)
            headers["Content-Type"] = f"multipart/form-data; boundary={boundary}"
        elif data is not None:
            payload = urllib.parse.urlencode(data).encode()
            headers["Content-Type"] = "application/x-www-form-urlencoded"
        request = urllib.request.Request(urllib.parse.urljoin(self.base, path), data=payload, headers=headers)
        try:
            response = (self.normal if follow else self.raw).open(request, timeout=30)
        except urllib.error.HTTPError as error:
            response = error
        return response.status, response.url, response.read(), response.headers

    def token(self, path):
        status, _, body, _ = self.request(path)
        assert status == 200, (path, status)
        return Inputs(body.decode()).values["__RequestVerificationToken"]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base", default="http://localhost:5136")
    parser.add_argument("--allow-synthetic-data", action="store_true")
    args = parser.parse_args()
    url = urllib.parse.urlparse(args.base)
    if not args.allow_synthetic_data or url.hostname not in {"localhost", "127.0.0.1", "::1"}:
        parser.error("Requires --allow-synthetic-data and a loopback URL with a synthetic database.")
    checks = []

    def check(condition, description):
        assert condition, description
        checks.append(description)
        print("PASS:", description)

    anonymous = Client(args.base)
    check(anonymous.request("/")[0] == 200, "Public landing page")
    for path in ["/Admin/Dashboard", "/Clients/Search", "/Documents/Identity?clientId=1", "/Documents/Screening?documentId=1"]:
        result = anonymous.request(path, follow=False)
        check(result[0] == 302 and "/Account/Login" in result[3].get("Location", ""), "Anonymous challenge: " + path.split('?')[0])
    check(anonymous.request("/uploads/identity_docs/example.pdf")[0] == 404, "Legacy upload URL blocked")

    def register(label):
        client = Client(args.base)
        password = "Synthetic!" + secrets.token_hex(12)
        national_id = "00" + str(secrets.randbelow(100_000_000)).zfill(8)
        token = client.token("/Account/Register")
        status, final_url, body, _ = client.request("/Account/Register", {
            "__RequestVerificationToken": token, "NationalId": national_id,
            "FullNameAr": label, "PhoneNumber": "0000000000",
            "Password": password, "ConfirmPassword": password})
        check(status == 200 and "/Clients/Details" in final_url, "Registration and profile: " + label)
        client_id = int(urllib.parse.parse_qs(urllib.parse.urlparse(final_url).query)["clientId"][0])
        return client, client_id

    owner, client_id = register("Synthetic Orchard Demo")
    other, other_id = register("Synthetic Access Test")
    result = owner.request("/Admin/Dashboard", follow=False)
    check(result[0] == 302 and "AccessDenied" in result[3].get("Location", ""), "Citizen denied administrator dashboard")
    result = other.request(f"/Clients/Details?clientId={client_id}", follow=False)
    check(result[0] == 302 and "AccessDenied" in result[3].get("Location", ""), "Other citizen denied client record")
    check(owner.request("/Screening/StartNewSession", {"clientId": client_id})[0] == 400, "Missing CSRF token rejected")
    token = owner.token(f"/Clients/Details?clientId={client_id}")
    status, session_url, _, _ = owner.request("/Screening/StartNewSession", {"clientId": client_id, "__RequestVerificationToken": token})
    check(status == 200 and "Instructions" in session_url, "Start screening with CSRF token")
    session_id = int(urllib.parse.parse_qs(urllib.parse.urlparse(session_url).query)["sessionId"][0])
    tool_path = f"/Screening/Tool?clientId={client_id}&sessionId={session_id}"
    token = owner.token(tool_path)
    check(owner.request(tool_path + "&currentSectionIndex=-1")[0] == 400, "Invalid section index rejected")
    result = owner.request("/Screening/ProcessSection", {"clientId": client_id, "sessionId": session_id,
        "currentSectionIndex": 0, "Answers[0].QuestionId": 999999, "Answers[0].Answer": "Invalid", "__RequestVerificationToken": token})
    check(result[0] == 400, "Tampered screening answer rejected")
    _, _, tool, _ = owner.request(tool_path)
    section_id = int(re.search(r'uploadSectionDocument\(this,\s*\d+,\s*(\d+)\)', tool.decode()).group(1))
    # A tiny synthetic signature fixture, never a real identity or customer document.
    sample = b"%PDF-1.4\n% SYNTHETIC PORTFOLIO TEST\n%%EOF"
    result = owner.request("/Screening/UploadDocument", {"sessionId": session_id, "sectionId": section_id,
        "__RequestVerificationToken": token}, file=("document", "synthetic.pdf", sample))
    uploaded = json.loads(result[2])
    check(uploaded.get("success") is True and uploaded.get("docId", 0) > 0, "Private document upload returns persistent ID")
    document_id = uploaded["docId"]
    result = owner.request(uploaded["path"])
    check(result[0] == 200 and result[2] == sample and "attachment" in result[3].get("Content-Disposition", ""), "Owner can download attachment")
    check(result[3].get("Cache-Control") == "no-store", "Document download is not cached")
    result = other.request(uploaded["path"], follow=False)
    check(result[0] == 302 and "AccessDenied" in result[3].get("Location", ""), "Other citizen cannot download document")

    edit_path = f"/Clients/EditProfile?clientId={client_id}"
    _, _, body, _ = owner.request(edit_path)
    edit_values = Inputs(body.decode()).values
    edit_values.update({"FullNameAr": "Synthetic Orchard Demo", "FullNameEn": "Synthetic Orchard Demo",
        "ApplicationUserId": "attempted-owner-change", "Region": "Amman"})
    result = owner.request("/Clients/EditProfile", edit_values, file=("identityFile", "synthetic-identity.pdf", sample))
    check(result[0] == 200 and "/Clients/Details" in result[1], "Identity upload and profile update")
    result = owner.request(f"/Documents/Identity?clientId={client_id}")
    check(result[0] == 200 and result[2] == sample, "Owner identity download; ownership field cannot be overposted")
    result = other.request(f"/Documents/Identity?clientId={client_id}", follow=False)
    check(result[0] == 302 and "AccessDenied" in result[3].get("Location", ""), "Other citizen denied identity document")

    for index in range(5):
        _, _, body, _ = owner.request(tool_path + f"&currentSectionIndex={index}")
        values = Inputs(body.decode()).values
        data = {"clientId": client_id, "sessionId": session_id, "currentSectionIndex": index,
            "__RequestVerificationToken": values["__RequestVerificationToken"]}
        for name, value in values.items():
            if name.endswith(".QuestionId"):
                data[name] = value
                data[name.replace(".QuestionId", ".Answer")] = "Yes"
        status, destination, body, _ = owner.request("/Screening/ProcessSection", data)
        check(status == 200, f"Save screening section {index + 1}")
    check("/Screening/Result" in destination, "Screening completes and shows stored result")
    check(owner.request("/Screening/ProcessSection", data)[0] == 409, "Completed screening rejects answer changes")
    token = owner.token(f"/Clients/Details?clientId={client_id}")
    check(owner.request("/Screening/DeleteDocument", {"documentId": document_id, "__RequestVerificationToken": token})[0] == 409,
        "Completed screening preserves documents")

    loan_data = {"ClientId": client_id, "SessionId": session_id, "LoanCategory": "SeasonalShortTerm",
        "Input.PrincipalAmount": 1000, "Input.DurationMonths": 12, "Input.PaymentFrequency": "Monthly",
        "Input.InterestType": "Simple", "Input.AnnualInterestRate": 0, "__RequestVerificationToken": token}
    status, destination, body, _ = owner.request("/Loans/CreateLoan", loan_data)
    check(status == 200 and "/Clients/Details" in destination, "Submit loan application with server-controlled rate")
    loan_match = re.search(r'/Loans/ApplicationDetails\?loanId=(\d+)', body.decode())
    check(loan_match is not None, "Submitted application appears in client profile")
    loan_id = int(loan_match.group(1))
    result = other.request(f"/Loans/ApplicationDetails?loanId={loan_id}", follow=False)
    check(result[0] == 302 and "AccessDenied" in result[3].get("Location", ""), "Other citizen denied loan application")

    admin_password = os.environ.get("DEMO_ADMIN_PASSWORD")
    if admin_password:
        staff = Client(args.base)
        token = staff.token("/Account/Login")
        result = staff.request("/Account/Login", {"NationalId": "admin", "Password": admin_password, "__RequestVerificationToken": token})
        check(result[0] == 200 and "Clients/Search" in result[1], "Administrator login")
        for path in ["/Admin/Dashboard", "/Admin/Sections", "/Admin/Questions", "/Admin/ManageUsers", "/Admin/AuditLogs", "/Admin/PendingLoans"]:
            result = staff.request(path)
            check(result[0] == 200 and urllib.parse.urlparse(result[1]).path == path, "Staff route: " + path)
        check(staff.request(uploaded["path"])[0] == 200, "Staff can download authorized document")

    Path(".local").mkdir(exist_ok=True)
    Path(".local/smoke-result.json").write_text(json.dumps({"checks": checks, "clientId": client_id,
        "otherClientId": other_id, "sessionId": session_id, "loanId": loan_id}, indent=2), encoding="utf-8")
    print(f"Completed {len(checks)} checks. Synthetic record identifiers saved in .local/smoke-result.json.")


if __name__ == "__main__":
    main()
