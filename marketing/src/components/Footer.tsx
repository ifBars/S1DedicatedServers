import { motion } from "framer-motion";
import { useScrollReveal } from "@/hooks/useScrollReveal";
import nightCity from "@/assets/banners/night-city.png";
import logoIcon from "@/assets/logo-icon.png";

const Footer = () => {
  const { ref, visible } = useScrollReveal(0.1);

  return (
    <>
      {/* Final CTA */}
      <section className="relative py-32 md:py-40 overflow-hidden">
        <div className="absolute inset-0">
          <img
            src={nightCity}
            alt=""
            className="w-full h-full object-cover"
            loading="lazy"
          />
          <div className="absolute inset-0 bg-background/90" />
        </div>

        <div ref={ref} className="relative z-10 container px-4 md:px-8 max-w-2xl mx-auto text-center">
          <motion.h2
            className="text-4xl md:text-5xl font-bold tracking-tight mb-6"
            initial={{ opacity: 0, y: 30 }}
            animate={visible ? { opacity: 1, y: 0 } : {}}
            transition={{ duration: 0.8, ease: [0.22, 1, 0.36, 1] }}
          >
            Ready to Host?
          </motion.h2>
          <motion.p
            className="text-muted-foreground text-lg mb-10 max-w-md mx-auto"
            initial={{ opacity: 0, y: 20 }}
            animate={visible ? { opacity: 1, y: 0 } : {}}
            transition={{ duration: 0.7, delay: 0.1, ease: [0.22, 1, 0.36, 1] }}
          >
            Download, configure, and launch your dedicated server in minutes.
          </motion.p>
          <motion.div
            className="flex items-center justify-center gap-4"
            initial={{ opacity: 0, y: 20 }}
            animate={visible ? { opacity: 1, y: 0 } : {}}
            transition={{ duration: 0.6, delay: 0.2, ease: [0.22, 1, 0.36, 1] }}
          >
            <a
              href="https://github.com/ifBars/S1DedicatedServers/releases"
              target="_blank"
              rel="noopener noreferrer"
              className="inline-flex items-center justify-center rounded-md bg-primary px-7 py-3 text-sm font-semibold text-primary-foreground hover:bg-primary/90 transition-all duration-200 hover:shadow-[0_0_30px_-5px_hsl(var(--primary)/0.4)]"
            >
              Get Started
            </a>
            <a
              href="https://ifbars.github.io/S1DedicatedServers/docs/index.html"
              target="_blank"
              rel="noopener noreferrer"
              className="inline-flex items-center justify-center rounded-md border border-border/50 px-7 py-3 text-sm font-medium text-foreground hover:bg-secondary/50 transition-colors duration-200"
            >
              Documentation
            </a>
          </motion.div>
        </div>
      </section>

      {/* Footer */}
      <footer className="border-t border-border/20 py-10">
        <div className="container px-4 md:px-8 max-w-6xl mx-auto">
          <div className="flex flex-col md:flex-row items-center justify-between gap-6">
            <div className="flex items-center gap-2.5">
              <img src={logoIcon} alt="" className="w-5 h-5 opacity-50" />
              <span className="text-xs text-muted-foreground">S1DedicatedServers</span>
            </div>
            <div className="flex items-center gap-6">
              <a href="https://github.com/ifBars/S1DedicatedServers" target="_blank" rel="noopener noreferrer" className="text-xs text-muted-foreground hover:text-foreground transition-colors">GitHub</a>
              <a href="https://ifbars.github.io/S1DedicatedServers/docs/index.html" target="_blank" rel="noopener noreferrer" className="text-xs text-muted-foreground hover:text-foreground transition-colors">Docs</a>
              <a href="https://github.com/ifBars/S1DedicatedServers/releases" target="_blank" rel="noopener noreferrer" className="text-xs text-muted-foreground hover:text-foreground transition-colors">Releases</a>
            </div>
          </div>
          <div className="mt-8 pt-6 border-t border-border/15">
            <p className="text-[11px] text-muted-foreground/60 leading-relaxed max-w-2xl mx-auto text-center">
              S1DedicatedServers is an open-source, community-developed project.
              It is not affiliated with, endorsed by, or officially connected to
              TVGS, the developer and publisher of Schedule I.
              All game assets and trademarks belong to their respective owners.
            </p>
          </div>
        </div>
      </footer>
    </>
  );
};

export default Footer;
