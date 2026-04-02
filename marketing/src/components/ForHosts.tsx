import { motion } from "framer-motion";
import { useScrollReveal } from "@/hooks/useScrollReveal";
import gasMart from "@/assets/gas-mart.png";

const capabilities = [
  { label: "24/7 uptime", detail: "No dependency on a player to host. Your server runs headless, always on." },
  { label: "Admin & permissions", detail: "Granular operator roles, permission controls, kick and ban management." },
  { label: "Remote console", detail: "Full TCP-based console for headless management from anywhere." },
  { label: "Deep configurability", detail: "Every aspect of server behavior is adjustable through configuration files." },
  { label: "Optional web panel", detail: "A localhost panel for real-time monitoring and local operations." },
];

const ForHosts = () => {
  const { ref, visible } = useScrollReveal(0.1);

  return (
    <section id="for-hosts" className="py-32 md:py-40">
      <div ref={ref} className="container px-4 md:px-8 max-w-6xl mx-auto">
        <div className="grid lg:grid-cols-2 gap-16 lg:gap-24 items-start">
          {/* Text column */}
          <motion.div
            initial={{ opacity: 0, y: 40 }}
            animate={visible ? { opacity: 1, y: 0 } : {}}
            transition={{ duration: 0.8, ease: [0.22, 1, 0.36, 1] }}
          >
            <p className="text-xs font-medium tracking-[0.25em] uppercase text-primary mb-4">
              For Server Owners
            </p>
            <h2 className="text-4xl md:text-5xl font-bold tracking-tight leading-[1.05] mb-6">
              Your Server,
              <br />
              Your Rules
            </h2>
            <p className="text-muted-foreground text-lg leading-relaxed mb-12 max-w-md">
              Run a persistent, authoritative server you fully control.
              No more relying on a player to stay connected.
            </p>

            <div className="space-y-0">
              {capabilities.map(({ label, detail }, i) => (
                <motion.div
                  key={label}
                  className="py-5 border-t border-border/30 group"
                  initial={{ opacity: 0, x: -20 }}
                  animate={visible ? { opacity: 1, x: 0 } : {}}
                  transition={{
                    duration: 0.5,
                    delay: 0.2 + i * 0.08,
                    ease: [0.22, 1, 0.36, 1],
                  }}
                >
                  <h4 className="text-sm font-semibold text-foreground mb-1 group-hover:text-primary transition-colors duration-200">
                    {label}
                  </h4>
                  <p className="text-sm text-muted-foreground leading-relaxed">
                    {detail}
                  </p>
                </motion.div>
              ))}
              <div className="border-t border-border/30" />
            </div>
          </motion.div>

          {/* Image column */}
          <motion.div
            className="relative lg:sticky lg:top-32"
            initial={{ opacity: 0, scale: 0.97 }}
            animate={visible ? { opacity: 1, scale: 1 } : {}}
            transition={{ duration: 1, delay: 0.2, ease: [0.22, 1, 0.36, 1] }}
          >
            <div className="relative rounded-lg overflow-hidden">
              <img
                src={gasMart}
                alt="Schedule I Gas-Mart at night"
                className="w-full h-auto"
                loading="lazy"
              />
              <div className="absolute inset-0 bg-gradient-to-t from-background/50 to-transparent" />
            </div>
          </motion.div>
        </div>
      </div>
    </section>
  );
};

export default ForHosts;
