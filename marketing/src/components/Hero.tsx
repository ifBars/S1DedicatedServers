import { useEffect, useRef, useState } from "react";
import { motion } from "framer-motion";
import heroImg from "@/assets/hero-town.jpg";

const Hero = () => {
  const sectionRef = useRef<HTMLElement>(null);
  const [scrollY, setScrollY] = useState(0);

  useEffect(() => {
    const prefersReducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    if (prefersReducedMotion) return;

    const onScroll = () => setScrollY(window.scrollY);
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => window.removeEventListener("scroll", onScroll);
  }, []);

  const imgParallax = scrollY * 0.3;
  const overlayOpacity = Math.min(0.9, 0.6 + scrollY * 0.0008);

  return (
    <section
      ref={sectionRef}
      className="relative min-h-screen flex items-center justify-center overflow-hidden"
    >
      {/* Parallax background */}
      <div className="absolute inset-0">
        <img
          src={heroImg}
          alt="Schedule I town overview"
          className="w-full h-full object-cover will-change-transform"
          style={{ transform: `translateY(${imgParallax}px) scale(1.1)` }}
          loading="eager"
        />
        <div
          className="absolute inset-0 bg-background"
          style={{ opacity: overlayOpacity }}
        />
        <div className="absolute inset-0 bg-gradient-to-b from-transparent via-transparent to-background" />
      </div>

      {/* Content */}
      <div className="relative z-10 container px-4 md:px-8 max-w-3xl mx-auto text-center pt-24 pb-32">
        <motion.div
          initial={{ opacity: 0, y: 30 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.8, ease: [0.22, 1, 0.36, 1] }}
        >
          <p className="text-xs font-medium tracking-[0.25em] uppercase text-muted-foreground mb-8">
            Open Source &middot; Beta
          </p>
        </motion.div>

        <motion.h1
          className="text-5xl sm:text-6xl md:text-7xl lg:text-8xl font-extrabold tracking-tight leading-[0.95] mb-8"
          initial={{ opacity: 0, y: 40 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 1, delay: 0.15, ease: [0.22, 1, 0.36, 1] }}
        >
          Dedicated Servers
          <br />
          <span className="text-gradient">for Schedule&nbsp;I</span>
        </motion.h1>

        <motion.p
          className="text-lg md:text-xl text-muted-foreground max-w-xl mx-auto mb-12 leading-relaxed"
          initial={{ opacity: 0, y: 30 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.8, delay: 0.35, ease: [0.22, 1, 0.36, 1] }}
        >
          Authoritative headless servers with full admin tooling,
          remote console, and a public mod API — built by the community.
        </motion.p>

        <motion.div
          className="flex items-center justify-center gap-4"
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.7, delay: 0.55, ease: [0.22, 1, 0.36, 1] }}
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
            href="https://github.com/ifBars/S1DedicatedServers"
            target="_blank"
            rel="noopener noreferrer"
            className="inline-flex items-center justify-center rounded-md border border-border/50 px-7 py-3 text-sm font-medium text-foreground hover:bg-secondary/50 transition-colors duration-200"
          >
            GitHub
          </a>
        </motion.div>
      </div>

      {/* Bottom gradient transition */}
      <div className="absolute bottom-0 left-0 right-0 h-40 bg-gradient-to-t from-background to-transparent pointer-events-none" />
    </section>
  );
};

export default Hero;
