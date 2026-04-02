import { motion } from "framer-motion";
import { useScrollReveal } from "@/hooks/useScrollReveal";

const steps = [
  { num: "01", title: "Download", desc: "Grab the latest build from GitHub Releases. Compatible with BepInEx." },
  { num: "02", title: "Install", desc: "Follow the setup docs for BepInEx, file placement, and initial config." },
  { num: "03", title: "Configure", desc: "Adjust permissions, auth backends, networking, and server behavior." },
  { num: "04", title: "Launch", desc: "Start headless, connect via remote console or web panel, invite your community." },
];

const GettingStarted = () => {
  const { ref, visible } = useScrollReveal(0.1);

  return (
    <section id="getting-started" className="py-32 md:py-40">
      <div ref={ref} className="container px-4 md:px-8 max-w-4xl mx-auto">
        <motion.div
          className="text-center mb-20"
          initial={{ opacity: 0, y: 30 }}
          animate={visible ? { opacity: 1, y: 0 } : {}}
          transition={{ duration: 0.8, ease: [0.22, 1, 0.36, 1] }}
        >
          <h2 className="text-4xl md:text-5xl font-bold tracking-tight mb-4">
            Up and Running in Minutes
          </h2>
          <p className="text-muted-foreground text-lg max-w-lg mx-auto">
            From download to a live server in four steps.
          </p>
        </motion.div>

        <div className="relative">
          {/* Vertical line */}
          <div className="absolute left-6 md:left-8 top-0 bottom-0 w-px bg-border/30" />

          <div className="space-y-0">
            {steps.map(({ num, title, desc }, i) => (
              <motion.div
                key={num}
                className="relative flex gap-8 md:gap-12 py-8 group"
                initial={{ opacity: 0, y: 20 }}
                animate={visible ? { opacity: 1, y: 0 } : {}}
                transition={{
                  duration: 0.5,
                  delay: 0.15 + i * 0.1,
                  ease: [0.22, 1, 0.36, 1],
                }}
              >
                {/* Step number */}
                <div className="relative z-10 w-12 md:w-16 shrink-0 flex items-start justify-center">
                  <span className="text-2xl md:text-3xl font-bold text-primary/30 group-hover:text-primary/60 transition-colors duration-300 font-mono">
                    {num}
                  </span>
                </div>

                {/* Content */}
                <div className="pt-1">
                  <h3 className="text-lg font-semibold mb-1 group-hover:text-primary transition-colors duration-200">
                    {title}
                  </h3>
                  <p className="text-sm text-muted-foreground leading-relaxed max-w-md">
                    {desc}
                  </p>
                </div>
              </motion.div>
            ))}
          </div>
        </div>

        <motion.div
          className="mt-16 text-center"
          initial={{ opacity: 0, y: 20 }}
          animate={visible ? { opacity: 1, y: 0 } : {}}
          transition={{ duration: 0.6, delay: 0.7, ease: [0.22, 1, 0.36, 1] }}
        >
          <a
            href="https://github.com/ifBars/S1DedicatedServers/releases"
            target="_blank"
            rel="noopener noreferrer"
            className="inline-flex items-center justify-center rounded-md bg-primary px-7 py-3 text-sm font-semibold text-primary-foreground hover:bg-primary/90 transition-all duration-200 hover:shadow-[0_0_30px_-5px_hsl(var(--primary)/0.4)]"
          >
            Download Latest Release
          </a>
        </motion.div>
      </div>
    </section>
  );
};

export default GettingStarted;
