import { motion } from "framer-motion";
import { Check } from "lucide-react";
import { useScrollReveal } from "@/hooks/useScrollReveal";
import docsSite from "@/assets/docs-site.png";

const apis = [
  {
    title: "Server & Client Mod APIs",
    desc: "Well-defined extension points for both server-side logic and client-side features.",
  },
  {
    title: "Lifecycle Hooks",
    desc: "Hook into server start, stop, player join and leave, and other runtime events.",
  },
  {
    title: "Persistence Integration",
    desc: "Integrate custom data with the save and load system without fighting the core server flow.",
  },
  {
    title: "Custom Messaging",
    desc: "Send and receive custom network messages between server code and connected clients.",
  },
  {
    title: "Contribution Friendly",
    desc: "Build against an open-source codebase with docs, examples, and an active improvement loop.",
  },
];

const ForDevelopers = () => {
  const { ref, visible } = useScrollReveal(0.1);

  return (
    <section id="for-developers" className="py-24 md:py-24">
      <div ref={ref} className="container mx-auto max-w-7xl px-4 md:px-8">
        <div className="grid items-start gap-14 lg:grid-cols-[minmax(520px,700px)_minmax(0,1fr)] lg:gap-20">
          <motion.div
            className="order-2 lg:order-1 relative lg:sticky lg:top-28"
            initial={{ opacity: 0, scale: 0.97 }}
            animate={visible ? { opacity: 1, scale: 1 } : {}}
            transition={{ duration: 1, delay: 0.2, ease: [0.22, 1, 0.36, 1] }}
          >
            <div className="relative overflow-hidden rounded-2xl border border-border/40 bg-muted/10 shadow-[0_24px_80px_hsl(0_0%_0%/0.32)] lg:aspect-[16/10]">
              <div className="absolute inset-x-0 top-0 z-10 h-px bg-gradient-to-r from-transparent via-primary/55 to-transparent" />
              <img
                src={docsSite}
                className="h-auto w-full object-contain lg:h-full lg:w-full lg:object-cover lg:object-left-top"
                loading="lazy"
              />
              <div className="absolute inset-0 bg-gradient-to-t from-background/55 via-background/0 to-background/10" />
            </div>
          </motion.div>

          <motion.div
            className="order-1 lg:order-2 max-w-xl"
            initial={{ opacity: 0, y: 40 }}
            animate={visible ? { opacity: 1, y: 0 } : {}}
            transition={{ duration: 0.8, ease: [0.22, 1, 0.36, 1] }}
          >
            <p className="mb-4 text-xs font-medium uppercase tracking-[0.25em] text-primary">
              For Developers
            </p>
            <h2 className="mb-2 text-4xl font-bold leading-[1.02] tracking-tight md:text-5xl">
              A Platform
              <br />
              Not Just a Mod
            </h2>

            <div className="border-t border-border/30 pt-4">
              <p className="mb-5 text-sm font-semibold text-foreground/90">
                Extension points for server mods, client features, and custom workflows
              </p>
              <ul className="space-y-4">
                {apis.map(({ title, desc }, i) => (
                  <motion.li
                    key={title}
                    className="flex items-start gap-4"
                    initial={{ opacity: 0, x: 20 }}
                    animate={visible ? { opacity: 1, x: 0 } : {}}
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
                        {title}
                      </h4>
                      <p className="text-sm leading-relaxed text-muted-foreground">
                        {desc}
                      </p>
                    </div>
                  </motion.li>
                ))}
              </ul>
            </div>
          </motion.div>
        </div>
      </div>
    </section>
  );
};

export default ForDevelopers;
