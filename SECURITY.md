# Security Policy

Thank you for taking the time to responsibly disclose security issues affecting Notepad Pro. This document explains how to report a vulnerability, what to expect in our response, and our disclosure policy.

## Supported Versions
- We provide security fixes for the latest stable release and the previous minor release where practical. If you're unsure whether your version is supported, report the issue and include the app version and commit SHA (if known).

## Reporting a Vulnerability
Preferred reporting methods (in order):

1. GitHub Security Advisories: https://github.com/AnotherLaughingMan/NotepadPro/security/advisories
2. Email: security@notepadpro.example (replace with the maintainer contact or use the repository's security contact)

If you use email, you may encrypt your report with our PGP key. If you need the key or instructions for encrypted submission, request it via the repository's security contact.

When reporting, please include:
- A clear summary of the issue and its impact
- Affected versions and environment (OS, runtime)
- Steps to reproduce, proof-of-concept or exploit code (minimal reproducer)
- Any suggested mitigation or patch ideas

Do not post details publicly until the issue has been addressed and coordinated disclosure has occurred.

## Triage and Response
- Acknowledgement: We will acknowledge receipt within 48 hours whenever possible.
- Triage: We aim to triage and assign a severity within 7 days.
- Fixing: Critical or high-severity issues will be prioritized. We will work with the reporter to produce a fix and releases or patches.

If you do not receive an acknowledgement within 48 hours, re-send your report or open a private support channel via the repository contact.

## Coordinated Disclosure Policy
- We follow coordinated disclosure: reporters agree to give maintainers time to fix the issue before public disclosure.
- Typical disclosure window: up to 90 days from the initial private report. We may shorten or extend this timeline depending on the severity and complexity, and will communicate the timeline to the reporter.
- For critical vulnerabilities actively exploited in the wild, we will prioritize mitigation and may coordinate faster disclosure and CVE assignment.

## CVE and Credits
- We will request a CVE for issues that meet the criteria for a CVE assignment (typically moderate or higher severity).
- We will credit researchers who report issues in release notes or security advisories unless the reporter requests anonymity.

## Safe Harbor / Researcher Guidelines
- Please act in good faith and avoid privacy violations, data exfiltration, or degradation of production services during testing.
- Avoid social engineering or any interaction that could expose user data.

## Dependency Vulnerabilities
If the issue is in a third-party dependency, please report it via that project's preferred channel and notify us so we can coordinate a fix and update affected dependencies.

## Contact
Use GitHub Security Advisories or email the security contact listed above. Replace `security@notepadpro.example` with the project's published security contact if one exists.

---
If you are a maintainer: update this file with a working email address, PGP key details (fingerprint), and any project-specific support channels.
