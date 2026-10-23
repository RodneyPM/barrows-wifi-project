# Barrows WiFi Technical Assessment

Software Engineer · Version 2.1 · October 2026

## About this assessment

Barrows WiFi runs free WiFi and retail media across informal trade venues in South Africa: taverns, spaza shops, salons and forecourts. People connect through a captive portal on their phones, watch brand content and answer surveys. Brands and banks buy media and reporting on top of that data.

We are bringing the platform in-house from an outsourced development partner. Whoever joins will take over code they did not write, across the API, the databases, the web front ends and the data pipelines. This assessment is that job in miniature.

The repo is modelled on our platform. It is not our production code, and every brand, outlet, person and phone number in it is made up. It contains:

- a .NET 8 Minimal API on SQL Server with EF Core, JWT and API-key auth, and a MassTransit message bus
- a React, TypeScript, Vite and Tailwind campaign portal for clients
- the React captive portal that consumers see when they join the WiFi at an outlet
- a messy outlet file from a national distributor

It runs on a laptop. It has the kinds of problems real inherited code has, including some nobody has reported yet.

**This is a hard assessment on purpose.** There is more here than most people will finish. We want to see how you read unfamiliar code, how you prove a fix works, what you decide matters most, and how you explain it. Use AI as much as you like. We do.

Our users are on low-end Android phones, often on slow or patchy connections, and frequently inside the phone's "Sign in to network" window rather than a full browser. Keep that in mind throughout.

## Getting set up

1. Clone the starter repository: **https://github.com/musazwane-barrows/barrows-wifi-assessment**. Please don't fork it on GitHub, because forks of a public repository are public. Instead, create an empty **private** repository in your own GitHub account and push your clone to it, then work there:

        git clone https://github.com/musazwane-barrows/barrows-wifi-assessment.git
        cd barrows-wifi-assessment
        git remote set-url origin https://github.com/<your-username>/<your-private-repo>.git
        git push -u origin main
2. Read `README.md` for how to run it, then `HANDOVER.md`, the note the previous partner left.
3. Start everything with `docker compose up --build`. Or run SQL Server in Docker, the API with `dotnet run`, and the two web apps with `npm run dev`. The first start creates the database and loads seed data, which takes about a minute.
4. API and Swagger: http://localhost:5080/swagger. Campaign portal: http://localhost:5173. Captive portal: http://localhost:5174/?stp=835903. All seed users share the password `Assessment#2026`.

You need Docker, the .NET 8 SDK and Node 22. If setup blocks you for more than 20 minutes, email musa.zwane@barrows.co. Setup is not part of the test, but how you handle a problem is worth telling us about.

## The tickets

Six tickets have come in from support, clients and the product team. Do them in the order you think is right and tell us why. If you don't get to everything, tell us what's left and how you would approach it.

### Ticket 1: A client can see another client's report

> From the Shisa Lager account manager: "Our ops person opened a link from a colleague and landed on a campaign report for Kwela Cola, a competitor. She could see all their numbers. How is that possible?"

Find the cause and fix it. Decide whether the fix should stop at this one place, and explain your reasoning. Add a test that would have caught it.

### Ticket 2: Impression numbers are too high

> From Kwela Cola's media agency: "Your report for Summer Refresh (campaign 1) says roughly 4,700 ad completes. Our ad-server log for the same flight is around 3,600. That's nearly 30% out, and we're invoicing off this."

Find out why the numbers differ and fix the report. Assume the agency's log is right. Show us how you proved what was wrong, not only the fix. Then stop the problem coming back at its source, wherever that turns out to be.

### Ticket 3: Post-campaign summary email (KES-212)

When a campaign ends, every `ClientAdmin` in that client's organisation should get one email with the campaign summary, and the summary should be stored in `CampaignSummaries`. The previous team wired up the `CampaignEnded` message and a stub consumer. Finish it.

- The client gets the summary **once**. Messages can be delivered more than once, and the mail relay sometimes fails to acknowledge a message it has actually sent. The local `FakeReportMailer` behaves this way on purpose.
- The numbers in the email match the corrected report from Ticket 2.
- To test it locally, sign in as `ops@ubuntubank.test`, set the end date of live campaign 3 a minute or two ahead, and watch the logs and the `SentEmails` table.

Tell us what guarantees your design gives and what it doesn't.

### Ticket 4: The campaign wizard is painful (`web/admin`, KES-251)

> From a client's campaign manager: "If I go back a step to fix something, everything I typed on that step is gone. Yesterday I clicked Create twice because nothing seemed to happen and now we have two campaigns. And the outlet list is unusable on my phone."

Fix the wizard. It should keep what the user entered when they move between steps, show mistakes on the step where they were made, and never create duplicates. It must work at 360px wide with the network throttled to Slow 3G in DevTools.

Check the values the wizard sends to the API carefully. The API treats `endsAt` as exclusive, and every outlet is in South Africa (SAST, UTC+2).

The outlet picker downloads all 1,600 outlets at once. Add an API endpoint that searches outlets by name, STP number or province and returns results a page at a time, and use it in the picker. Follow the conventions you find in the existing code. Add tests for the logic you change.

### Ticket 5: Blank captive portal on some phones (`web/portal`, KES-255)

> From support: "Owners keep reporting that customers see a blank white screen. It's mostly older phones, a Samsung Galaxy J2 and a couple of Huawei Y-series, and some say it happens in the little 'Sign in to Wi-Fi' window that pops up but not when they open Chrome. Customers also say it takes forever to load, and a few were asked the same survey question twice."

Find out why, fix what you can, and explain what you found. You won't have the phones, so tell us how you reasoned about it and how you would confirm it in the field.

### Ticket 6: Outlet master data from the distributor (KES-260)

Once a month a national distributor sends us their outlet list (`data/national-outlet-master.csv`). We need to reconcile it against our outlets (`data/outlets-current.csv`, an export of our table).

Write a script in Python or TypeScript that reports which records match ours, which are new outlets, which conflict with what we hold, and which should be rejected. Every record needs a reason. The file is messy in the ways these files usually are.

There is no single correct answer. We care about rules that are explicit, sensible for the business and tested. Put your matching rules and their trade-offs in a short README next to the script. Don't write anything to the database.

## Your handover notes

Before you finish, add `HANDOVER-NOTES.md` at the root of the repo. Imagine it's your second week, you've just done these tickets, and you're writing to the Head of Product about the platform you now own. Cover:

1. **What else you would fix, in priority order, and why.** Anything you noticed that nobody asked about: security, data, reliability, or things that hurt users on cheap phones and slow networks. Be specific about where it is and what could happen. You don't need to fix these.
2. **Anything in `HANDOVER.md` you would question** before relying on it.
3. **What you would do in your first month** if this platform were yours.

One or two pages is plenty.

### AI use

Use whatever tools you normally use, including AI assistants. We use Claude Code daily. Add a short section to your notes covering what you used AI for, anything it got wrong or that you changed, and anything you deliberately did yourself. There is no penalty for using AI. There is a penalty for submitting code you can't explain.

## Time, rules and submitting

**Time:** you have 7 days from the day you receive this brief. Spend what you think the work deserves. Unfinished is fine: fewer tickets done well beat all six done badly.

**What we look at:**

| Area | What good looks like |
| --- | --- |
| Understanding before changing | You found the real causes, not just the symptoms |
| Verification | Tests or queries that prove each fix; numbers checked |
| Security and data integrity | Fixes that hold across the codebase, not one endpoint |
| Reliability | An honest account of what your messaging design guarantees |
| Real users, real phones | Front-end decisions that work on low-end Android and slow networks |
| Data handling | Explicit, tested matching rules with a reason per record |
| Ownership | Useful findings in your handover notes that nobody asked for |
| Communication | Commits, notes and explanations a teammate can follow |

**Submitting:** push your work to your private repository, add the GitHub user **musazwane-barrows** as a collaborator (Settings, Collaborators, Add people), and email the repository link to musa.zwane@barrows.co. Make sure:

- Your commits show how the work progressed. Small commits with clear messages beat one big one.
- `HANDOVER-NOTES.md` is at the root, and the ingest script has its README.
- The project still starts with `docker compose up --build`, `dotnet test` passes, `npm test` passes in `web/admin`, and both web apps build with `npm run build`.

Please don't share the repo or this brief publicly.

## What happens next

If your submission moves forward, we'll book a 75-minute follow-up. It's a conversation, not a grilling.

1. **Walkthrough (25 min).** You take us through what you found, what you changed and how you know it works.
2. **A live change (20 min).** We ask for a small change to your code. Use your normal tools, AI included, and talk us through it.
3. **Data and design (20 min).** A reporting query on paper, and a conversation about running this platform and the people who use it.
4. **Your questions (10 min).**

Good luck. We're looking forward to seeing how you think.
