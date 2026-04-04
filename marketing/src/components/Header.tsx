import { useState, useEffect } from "react";
import logoIcon from "@/assets/logo-icon.png";
import { Menu, X } from "lucide-react";

const navLinks = [
  { label: "For Hosts", href: "#for-hosts" },
  { label: "For Developers", href: "#for-developers" },
  { label: "Getting Started", href: "#getting-started" },
  { label: "Docs", href: "https://docs.s1servers.com/" },
];

const Header = () => {
  const [mobileOpen, setMobileOpen] = useState(false);
  const [scrolled, setScrolled] = useState(false);

  const desktopLinkClass = scrolled
    ? "text-[13px] text-muted-foreground hover:text-foreground"
    : "text-[13px] text-muted-foreground/90 hover:text-foreground";

  const mobileLinkClass = "text-sm text-muted-foreground hover:text-foreground";

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 40);
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => window.removeEventListener("scroll", onScroll);
  }, []);

  return (
    <header
      className={`fixed top-0 left-0 right-0 z-50 transition-all duration-500 ${scrolled
        ? "bg-background/80 backdrop-blur-xl border-b border-border/30"
        : "bg-transparent"
        }`}
    >
      <div className="container relative flex items-center justify-between h-16 px-4 md:px-8">
        <a href="#" className="flex items-center gap-2.5 shrink-0">
          <img src={logoIcon} alt="S1DedicatedServers logo" className="w-7 h-7" />
          <span className="font-semibold text-foreground text-sm tracking-tight">
            S1DS
          </span>
        </a>

        <nav className="hidden lg:flex items-center gap-8 absolute left-1/2 -translate-x-1/2">
          {navLinks.map((l) => (
            <a
              key={l.href}
              href={l.href}
              target={l.href.startsWith("http") ? "_blank" : undefined}
              rel={l.href.startsWith("http") ? "noopener noreferrer" : undefined}
              className={`${desktopLinkClass} transition-colors duration-200`}
            >
              {l.label}
            </a>
          ))}
        </nav>

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
              target={l.href.startsWith("http") ? "_blank" : undefined}
              rel={l.href.startsWith("http") ? "noopener noreferrer" : undefined}
              onClick={() => setMobileOpen(false)}
              className={`block py-2.5 ${mobileLinkClass} transition-colors duration-200`}
            >
              {l.label}
            </a>
          ))}
        </div>
      )}
    </header>
  );
};

export default Header;
