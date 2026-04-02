import { useState, useEffect } from "react";
import logoIcon from "@/assets/logo-icon.png";
import { Menu, X } from "lucide-react";

const navLinks = [
  { label: "For Hosts", href: "#for-hosts" },
  { label: "For Developers", href: "#for-developers" },
  { label: "Getting Started", href: "#getting-started" },
  { label: "Docs", href: "#docs" },
  { label: "FAQ", href: "#faq" },
];

const Header = () => {
  const [mobileOpen, setMobileOpen] = useState(false);
  const [scrolled, setScrolled] = useState(false);

  const desktopLinkClass = scrolled
    ? "text-[13px] text-foreground/72 hover:text-foreground"
    : "text-[13px] text-foreground/88 hover:text-foreground";

  const mobileLinkClass = "text-sm text-foreground/82 hover:text-foreground";

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 40);
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => window.removeEventListener("scroll", onScroll);
  }, []);

  return (
    <header
      className={`fixed top-0 left-0 right-0 z-50 transition-all duration-500 ${
        scrolled
          ? "bg-background/80 backdrop-blur-xl border-b border-border/30"
          : "bg-transparent"
      }`}
    >
      <div className="container flex items-center justify-between h-16 px-4 md:px-8">
        <a href="#" className="flex items-center gap-2.5 shrink-0">
          <img src={logoIcon} alt="S1DedicatedServers" className="w-7 h-7" />
          <span className="font-semibold text-foreground text-sm tracking-tight">
            S1DS
          </span>
        </a>

        <nav className="hidden lg:flex items-center gap-8">
          {navLinks.map((l) => (
            <a
              key={l.href}
              href={l.href}
              className={`${desktopLinkClass} transition-colors duration-200`}
            >
              {l.label}
            </a>
          ))}
        </nav>

        <div className="hidden lg:flex items-center gap-4">
          <a
            href="https://github.com/ifBars/S1DedicatedServers"
            target="_blank"
            rel="noopener noreferrer"
            className={`${desktopLinkClass} transition-colors duration-200`}
          >
            GitHub
          </a>
          <a
            href="https://github.com/ifBars/S1DedicatedServers/releases"
            target="_blank"
            rel="noopener noreferrer"
            className="inline-flex items-center justify-center rounded-md bg-primary px-4 py-1.5 text-[13px] font-medium text-primary-foreground hover:bg-primary/90 transition-colors"
          >
            Get Started
          </a>
        </div>

        <button
          onClick={() => setMobileOpen(!mobileOpen)}
          className="lg:hidden p-2 text-muted-foreground hover:text-foreground"
          aria-label="Toggle menu"
        >
          {mobileOpen ? <X size={20} /> : <Menu size={20} />}
        </button>
      </div>

      {mobileOpen && (
        <div className="lg:hidden bg-background/95 backdrop-blur-xl border-t border-border/30 px-4 pb-4 pt-2">
          {navLinks.map((l) => (
            <a
              key={l.href}
              href={l.href}
              onClick={() => setMobileOpen(false)}
              className={`block py-2.5 ${mobileLinkClass} transition-colors duration-200`}
            >
              {l.label}
            </a>
          ))}
          <div className="flex flex-col gap-2 mt-3 pt-3 border-t border-border/30">
            <a
              href="https://github.com/ifBars/S1DedicatedServers"
              target="_blank"
              rel="noopener noreferrer"
              className={mobileLinkClass}
            >
              GitHub
            </a>
            <a
              href="https://github.com/ifBars/S1DedicatedServers/releases"
              target="_blank"
              rel="noopener noreferrer"
              className="inline-flex items-center justify-center rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground"
            >
              Get Started
            </a>
          </div>
        </div>
      )}
    </header>
  );
};

export default Header;
