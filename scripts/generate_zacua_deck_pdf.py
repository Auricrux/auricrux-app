from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.pagesizes import landscape, letter
from reportlab.lib.units import inch
from reportlab.pdfgen import canvas


OUTPUT = Path("C:/repos/auricrux-app/FCA_Auricrux_Zacua_Deck_v2.pdf")
W, H = landscape(letter)


SLIDES = [
    (
        "Future Contractors of America",
        "AI-Powered Construction Operating System | Founder: Michael J. Bartholomew",
        [
            "One platform for leads, bids, projects, field execution, compliance, billing, and workforce readiness.",
            "Auricrux is the embedded intelligence layer guiding next-best actions across every workflow handoff.",
        ],
    ),
    (
        "Problem",
        "Construction operations are fragmented and high-friction",
        [
            "Critical workflows are split across disconnected tools, inboxes, and spreadsheets.",
            "Lead-to-bid-to-project handoffs break continuity and create avoidable rework.",
            "Institutional know-how is trapped in people rather than operational systems.",
        ],
    ),
    (
        "Solution",
        "FCA + Auricrux unifies operations and intelligence",
        [
            "A single construction operating system replacing point-solution sprawl.",
            "Connected workflows across estimating, projects, files, finance, customer comms, and training.",
            "AI-guided execution that turns operational data into real-time decisions.",
        ],
    ),
    (
        "Product",
        "Live platform architecture, not slideware",
        [
            "Public product shell + authenticated portal workflows are already live.",
            "Web and mobile operational surfaces support office-field continuity.",
            "FCA Ecosystem is the convergence path to replace legacy fragmented repos.",
        ],
    ),
    (
        "Why Now",
        "AI-native operations are becoming mandatory",
        [
            "Construction still has major workflow fragmentation and low software continuity.",
            "Modern AI systems can now provide reliable in-context operational guidance.",
            "Margin, compliance, and schedule pressure create immediate adoption urgency.",
        ],
    ),
    (
        "Go-To-Market",
        "Land in painful workflow bottlenecks, then expand",
        [
            "Start where continuity failures are most expensive: bid-to-project transition.",
            "Win by improving execution confidence in docs, communication, and billing readiness.",
            "Expand account depth via Academy/training and cross-module adoption.",
        ],
    ),
    (
        "Progress",
        "Execution velocity and public build artifacts",
        [
            "Active product iteration with live routes, repos, and deployment workflows.",
            "Operating model proven across web, mobile, and ecosystem convergence work.",
            "Founder-led AI-native execution compresses build-test-iterate cycles.",
        ],
    ),
    (
        "Business Model",
        "SaaS expansion from workflow command to system-of-record behavior",
        [
            "Subscription platform priced to team size and operational depth.",
            "Expansion via additional workflow modules and intelligence capabilities.",
            "Long-term retention through embedded daily execution and training continuity.",
        ],
    ),
    (
        "Vision",
        "Build the default operating system for contractors",
        [
            "From fragmented software stacks to one continuously learning execution platform.",
            "Capture senior operator judgment and operationalize it for every team member.",
            "Make high-quality contractor operations repeatable, scalable, and auditable.",
        ],
    ),
    (
        "The Ask",
        "Partner support to accelerate growth and category leadership",
        [
            "Support pilot-to-scale adoption with high-quality contractor design partners.",
            "Accelerate product depth across compliance, finance, and customer continuity.",
            "Help build a category-defining AI-native company in construction.",
        ],
    ),
]



def draw_slide(c: canvas.Canvas, title: str, subtitle: str, bullets: list[str], page_num: int) -> None:
    c.setFillColor(colors.HexColor("#F7F9FC"))
    c.rect(0, 0, W, H, stroke=0, fill=1)

    c.setFillColor(colors.HexColor("#0C1830"))
    c.rect(0, H - 0.3 * inch, W, 0.3 * inch, stroke=0, fill=1)

    c.setFillColor(colors.HexColor("#0C1830"))
    c.setFont("Helvetica-Bold", 34)
    c.drawString(0.7 * inch, H - 1.0 * inch, title)

    c.setFillColor(colors.HexColor("#636D7E"))
    c.setFont("Helvetica", 16)
    c.drawString(0.7 * inch, H - 1.45 * inch, subtitle)

    y = H - 2.2 * inch
    c.setFillColor(colors.HexColor("#1C222C"))
    for bullet in bullets:
        c.setFont("Helvetica", 19)
        c.drawString(0.9 * inch, y, "•")
        text = c.beginText(1.2 * inch, y)
        text.setFont("Helvetica", 19)
        text.textLines(_wrap_text(bullet, 95))
        c.drawText(text)
        lines = _line_count(bullet, 95)
        y -= (0.52 + 0.28 * (lines - 1)) * inch

    c.setFillColor(colors.HexColor("#24597D"))
    c.setFont("Helvetica", 10)
    c.drawString(0.7 * inch, 0.35 * inch, "www.futurecontractorsofamerica.com")
    c.drawRightString(W - 0.7 * inch, 0.35 * inch, f"Page {page_num}")



def _wrap_text(text: str, max_chars: int) -> str:
    words = text.split()
    lines = []
    line = []
    n = 0
    for w in words:
        add = len(w) + (1 if line else 0)
        if n + add > max_chars:
            lines.append(" ".join(line))
            line = [w]
            n = len(w)
        else:
            line.append(w)
            n += add
    if line:
        lines.append(" ".join(line))
    return "\n".join(lines)



def _line_count(text: str, max_chars: int) -> int:
    return max(1, len(_wrap_text(text, max_chars).splitlines()))



def main() -> None:
    c = canvas.Canvas(str(OUTPUT), pagesize=landscape(letter))
    for idx, (title, subtitle, bullets) in enumerate(SLIDES, start=1):
        draw_slide(c, title, subtitle, bullets, idx)
        c.showPage()
    c.save()
    print(f"Created: {OUTPUT}")


if __name__ == "__main__":
    main()
