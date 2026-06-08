import { useEffect, useRef, useState } from "react";
import { motion } from "framer-motion";
import { Download, ExternalLink, Github } from "lucide-react";
import cybranceeLogo from "@/assets/cybrancee-logo-dark.png";

const heroModules = import.meta.glob("../assets/heros/*.{png,jpg,jpeg,webp,avif}", {
  eager: true,
  import: "default",
});

const heroImages = Object.entries(heroModules)
  .sort(([leftPath], [rightPath]) => leftPath.localeCompare(rightPath))
  .map(([, image]) => image as string);

const heroSlides = heroImages;
const HERO_ROTATION_MS = 6000;

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
            alt=""
            aria-hidden="true"
            className={`absolute inset-0 h-full w-full object-cover will-change-transform saturate-[0.92] brightness-[0.68] contrast-[1.08] transition-opacity ease-out ${index === activeHeroIndex ? "opacity-100" : "opacity-0"
              }`}
            style={{
              transform: `translateY(${imgParallax}px) scale(1.1)`,
              transitionDuration: "1800ms",
            }}
            loading={index === 0 ? "eager" : "lazy"}
          />
        ))}
        <div
          className="absolute inset-0 bg-background/45 backdrop-blur-[1.5px]"
          style={{ opacity: overlayOpacity }}
        />
        <div className="absolute inset-0 bg-[radial-gradient(ellipse_at_50%_42%,hsl(0_0%_7%/0.18)_0%,hsl(0_0%_7%/0.36)_42%,hsl(0_0%_7%/0.78)_100%)]" />
        <div className="absolute inset-0 bg-[linear-gradient(to_bottom,hsl(0_0%_7%/0.62)_0%,hsl(0_0%_7%/0.18)_26%,hsl(0_0%_7%/0.48)_66%,hsl(0_0%_7%/0.9)_100%)]" />
      </div>

      {/* Content */}
      <motion.div
        className="relative z-10 container mx-auto max-w-4xl px-4 pb-24 pt-24 text-center md:px-8 md:pt-28"
        initial={false}
        animate={{ y: [0, -5, 0] }}
        transition={{ duration: 10, ease: "easeInOut", repeat: Infinity }}
      >
        <motion.h1
          className="mb-9 flex flex-col items-center text-4xl font-extrabold leading-[0.93] tracking-tight [text-shadow:0_10px_36px_hsl(0_0%_7%/0.48)] sm:text-6xl md:text-7xl lg:text-[7rem]"
          initial={false}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 1, delay: 0.05, ease: [0.22, 1, 0.36, 1] }}
        >
          <span className="block md:whitespace-nowrap">Dedicated Servers</span>
          <span className="block text-primary">for Schedule&nbsp;I</span>
        </motion.h1>

        <motion.p
          className="text-lg md:text-xl text-foreground/88 max-w-xl mx-auto mb-12 leading-relaxed [text-shadow:0_8px_28px_hsl(0_0%_7%/0.42)]"
          initial={false}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.8, delay: 0.25, ease: [0.22, 1, 0.36, 1] }}
        >
          Run your Schedule I world 24/7, customize the experience
          and build the foundation for the community server you actually want.
        </motion.p>

        <motion.div
          className="flex flex-col items-center justify-center gap-4"
          initial={false}
          animate={{ opacity: 1, y: [0, -3, 0] }}
          transition={{
            opacity: { duration: 0.7, delay: 0.55, ease: [0.22, 1, 0.36, 1] },
            y: { duration: 6, ease: "easeInOut", repeat: Infinity },
          }}
        >
          <div className="flex items-center justify-center gap-4">
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
          </div>
          <a
            href="https://docs.s1servers.com/"
            target="_blank"
            rel="noopener noreferrer"
            className="text-sm font-medium text-foreground/80 underline decoration-primary/60 underline-offset-4 transition-colors hover:text-foreground"
          >
            Open the dedicated server docs
          </a>
          <a
            href="https://cybrancee.com/bars"
            target="_blank"
            rel="noopener noreferrer"
            className="mt-2 inline-flex w-full max-w-sm items-center justify-center gap-2 rounded-md border border-border/45 bg-background/35 px-4 py-2 text-sm font-medium text-foreground/86 backdrop-blur-md transition-colors hover:border-primary/55 hover:bg-background/50 hover:text-foreground sm:w-auto sm:max-w-full"
          >
            <img
              src={cybranceeLogo}
              alt="Cybrancee"
              className="h-4 w-auto max-w-[116px] shrink-0 object-contain"
              loading="eager"
            />
            <span className="min-w-0 truncate">Rent a Server</span>
            <ExternalLink className="h-3.5 w-3.5 shrink-0 text-muted-foreground" aria-hidden="true" />
          </a>
        </motion.div>
      </motion.div>

      {/* Bottom gradient transition */}
      <div className="absolute bottom-0 left-0 right-0 h-40 bg-gradient-to-t from-background to-transparent pointer-events-none" />
    </section>
  );
};

export default Hero;
