import { useEffect, useRef, useState } from "react";
import { motion } from "framer-motion";
import { Download, Github } from "lucide-react";

const heroModules = import.meta.glob("../assets/heros/*.{png,jpg,jpeg,webp,avif}", {
  eager: true,
  import: "default",
});

const heroImages = Object.entries(heroModules)
  .sort(([leftPath], [rightPath]) => leftPath.localeCompare(rightPath))
  .map(([, image]) => image as string);

const heroSlides = heroImages;
const HERO_ROTATION_MS = 6500;

const Hero = () => {
  const sectionRef = useRef<HTMLElement>(null);
  const [scrollY, setScrollY] = useState(0);
  const [activeHeroIndex, setActiveHeroIndex] = useState(0);

  useEffect(() => {
    const prefersReducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    if (prefersReducedMotion) return;

    const onScroll = () => setScrollY(window.scrollY);
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => window.removeEventListener("scroll", onScroll);
  }, []);

  useEffect(() => {
    const prefersReducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    if (prefersReducedMotion || heroSlides.length <= 1) return;

    const intervalId = window.setInterval(() => {
      setActiveHeroIndex((currentIndex) => (currentIndex + 1) % heroSlides.length);
    }, HERO_ROTATION_MS);

    return () => window.clearInterval(intervalId);
  }, []);

  const imgParallax = scrollY * 0.3;
  const overlayOpacity = Math.min(0.82, 0.52 + scrollY * 0.0007);

  return (
    <section
      ref={sectionRef}
      className="relative min-h-screen flex items-center justify-center overflow-hidden"
    >
      {/* Parallax background */}
      <div className="absolute inset-0">
        {heroSlides.map((heroImage, index) => (
          <img
            key={heroImage}
            src={heroImage}
            alt="Schedule I town overview"
            className={`absolute inset-0 h-full w-full object-cover will-change-transform saturate-[0.92] brightness-[0.68] contrast-[1.08] transition-opacity ease-out ${index === activeHeroIndex ? "opacity-100" : "opacity-0"
              }`}
            style={{
              transform: `translateY(${imgParallax}px) scale(1.1)`,
              transitionDuration: "1800ms",
            }}
            loading={index === 0 ? "eager" : "lazy"}
          />
        ))}
        <div className="absolute inset-0 bg-[radial-gradient(circle_at_50%_28%,hsl(var(--accent)/0.01),transparent_50%)]" />
        <div className="absolute inset-0 bg-[linear-gradient(to_bottom,hsl(0_0%_7%/0.02)_0%,hsl(0_0%_7%/0.01)_34%,hsl(0_0%_7%/0.5)_100%)]" />
      </div>

      {/* Content */}
      <div className="relative z-10 container px-4 md:px-8 max-w-4xl mx-auto text-center pt-24 pb-32">
        <motion.div
          initial={{ opacity: 0, y: 30 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.8, ease: [0.22, 1, 0.36, 1] }}
        >
          <p className="text-xs font-medium tracking-[0.25em] uppercase text-foreground/78 mb-8">
            Open Source &middot; Beta
          </p>
        </motion.div>

        <motion.h1
          className="mb-8 flex flex-col items-center text-4xl font-extrabold leading-[0.93] tracking-tight [text-shadow:0_10px_36px_hsl(0_0%_7%/0.48)] sm:text-6xl md:text-7xl lg:text-[7rem]"
          initial={{ opacity: 0, y: 40 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 1, delay: 0.15, ease: [0.22, 1, 0.36, 1] }}
        >
          <span className="block md:whitespace-nowrap">Dedicated Servers</span>
          <span className="block text-primary">for Schedule&nbsp;I</span>
        </motion.h1>

        <motion.p
          className="text-lg md:text-xl text-foreground/88 max-w-xl mx-auto mb-12 leading-relaxed [text-shadow:0_8px_28px_hsl(0_0%_7%/0.42)]"
          initial={{ opacity: 0, y: 30 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.8, delay: 0.35, ease: [0.22, 1, 0.36, 1] }}
        >
          Run your Schedule I world 24/7, customize the experience
          and build the foundation for the community server you actually want.
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
            className="inline-flex items-center justify-center rounded-md bg-primary px-7 py-3 text-sm font-semibold text-primary-foreground hover:bg-primary/90 transition-all duration-200 hover:shadow-[0_0_34px_-8px_hsl(var(--primary)/0.45)]"
          >
            <Download className="mr-2 h-4 w-4" aria-hidden="true" />
            Download
          </a>
          <a
            href="https://github.com/ifBars/S1DedicatedServers"
            target="_blank"
            rel="noopener noreferrer"
            className="inline-flex items-center justify-center rounded-md border border-border/60 bg-background/35 px-7 py-3 text-sm font-medium text-foreground hover:bg-background/55 transition-colors duration-200"
          >
            <Github className="mr-2 h-4 w-4" aria-hidden="true" />
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
