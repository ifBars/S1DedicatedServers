import { motion } from "framer-motion";
import { useScrollReveal } from "@/hooks/useScrollReveal";
import { useMounted } from "@/hooks/useMounted";
import gasMart from "@/assets/web-panel.png";
import { Check } from "lucide-react";

const capabilities = [
  { label: "24/7 uptime", detail: "Your server runs headless, always on." },
  { label: "Admin & permissions", detail: "Group based roles, permission controls, kick and ban management." },
  { label: "Remote console", detail: "Full TCP-based console for headless management from anywhere." },
  { label: "Deep configurability", detail: "Every aspect of server behavior is adjustable through configuration files." },
  { label: "Optional web panel", detail: "A localhost panel for real-time monitoring and local operations." },
];

const ForHosts = () => {
  const { ref, visible } = useScrollReveal(0.1);
  const mounted = useMounted();
  const sectionAnimation = mounted && !visible ? { opacity: 0, y: 40 } : { opacity: 1, y: 0 };
  const listItemAnimation = mounted && !visible ? { opacity: 0, x: -20 } : { opacity: 1, x: 0 };
  const imageAnimation = mounted && !visible ? { opacity: 0, scale: 0.97 } : { opacity: 1, scale: 1 };

  return (
    <section id="for-hosts" className="py-24 md:py-24">
      <div ref={ref} className="container px-4 md:px-8 max-w-7xl mx-auto">
        <div className="grid items-start gap-14 lg:grid-cols-[minmax(0,1fr)_minmax(520px,700px)] lg:gap-20">
          {/* Text column */}
          <motion.div
            initial={false}
            animate={sectionAnimation}
            transition={{ duration: 0.8, ease: [0.22, 1, 0.36, 1] }}
            className="max-w-xl"
          >
            <p className="text-xs font-medium tracking-[0.25em] uppercase text-primary mb-4">
              For Server Owners
            </p>
            <h2 className="text-4xl md:text-5xl font-bold tracking-tight leading-[1.02] mb-2">
              Your Server
              <br />
              Your Rules
            </h2>

            <div className="border-t border-border/30 pt-4">
              <p className="mb-5 text-sm font-semibold text-foreground/90">
                Everything you need to keep the server online and under control
              </p>
              <ul className="space-y-4">
                {capabilities.map(({ label, detail }, i) => (
                  <motion.li
                    key={label}
                    className="flex items-start gap-4"
                    initial={false}
                    animate={listItemAnimation}
                    transition={{
                      duration: 0.5,
                      delay: 0.2 + i * 0.08,
                      ease: [0.22, 1, 0.36, 1],
                    }}
                  >
                    <span className="mt-0.5 inline-flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-primary/15 text-primary ring-1 ring-primary/20">
                      <Check className="h-3.5 w-3.5" />
                    </span>
                    <div>
                      <h4 className="mb-1 text-base font-semibold text-foreground">
                        {label}
                      </h4>
                      <p className="text-sm leading-relaxed text-muted-foreground">
                        {detail}
                      </p>
                    </div>
                  </motion.li>
                ))}
              </ul>
            </div>
          </motion.div>

          {/* Image column */}
          <motion.div
            className="relative lg:sticky lg:top-28"
            initial={false}
            animate={imageAnimation}
            transition={{ duration: 1, delay: 0.2, ease: [0.22, 1, 0.36, 1] }}
          >
            <div className="relative overflow-hidden rounded-2xl border border-border/40 bg-muted/10 shadow-[0_24px_80px_hsl(0_0%_0%/0.32)] lg:aspect-[16/10]">
              <div className="absolute inset-x-0 top-0 z-10 h-px bg-gradient-to-r from-transparent via-primary/55 to-transparent" />
              <img
                src={gasMart}
                alt="S1DedicatedServers web panel showing dedicated server controls and live status"
                className="h-auto w-full object-contain lg:h-full lg:w-full lg:object-cover lg:object-left-top"
                loading="lazy"
              />
              <div className="absolute inset-0 bg-gradient-to-t from-background/55 via-background/0 to-background/10" />
            </div>
          </motion.div>
        </div>
      </div>
    </section>
  );
};

export default ForHosts;
