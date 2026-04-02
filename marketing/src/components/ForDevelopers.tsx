import { motion } from "framer-motion";
import { useScrollReveal } from "@/hooks/useScrollReveal";
import growRoom from "@/assets/Barn.png";

const apis = [
  { title: "Server & Client Mod APIs", desc: "Well-defined extension points for both server-side logic and client-side features." },
  { title: "Lifecycle Hooks", desc: "Hook into server start, stop, player join/leave, and more." },
  { title: "Persistence Integration", desc: "Integrate custom data with the save/load system." },
  { title: "Custom Messaging", desc: "Send and receive custom network messages between server and clients." },
  { title: "Contribution Friendly", desc: "Open-source codebase welcoming pull requests, issues, and community input." },
];

const ForDevelopers = () => {
  const { ref, visible } = useScrollReveal(0.1);

  return (
    <section id="for-developers" className="py-32 md:py-40 relative">
      <div className="absolute inset-0 bg-gradient-to-b from-secondary/10 via-transparent to-transparent pointer-events-none" />

      <div ref={ref} className="container px-4 md:px-8 max-w-6xl mx-auto relative z-10">
        <div className="grid lg:grid-cols-2 gap-16 lg:gap-24 items-start">
          {/* Image column — left on desktop */}
          <motion.div
            className="order-2 lg:order-1 relative lg:sticky lg:top-32"
            initial={{ opacity: 0, scale: 0.97 }}
            animate={visible ? { opacity: 1, scale: 1 } : {}}
            transition={{ duration: 1, delay: 0.2, ease: [0.22, 1, 0.36, 1] }}
          >
            <div className="relative rounded-lg overflow-hidden">
              <img
                src={growRoom}
                alt="Schedule I grow room"
                className="w-full h-auto"
                loading="lazy"
              />
              <div className="absolute inset-0 bg-gradient-to-t from-background/50 to-transparent" />
            </div>
          </motion.div>

          {/* Text column */}
          <motion.div
            className="order-1 lg:order-2"
            initial={{ opacity: 0, y: 40 }}
            animate={visible ? { opacity: 1, y: 0 } : {}}
            transition={{ duration: 0.8, ease: [0.22, 1, 0.36, 1] }}
          >
            <p className="text-xs font-medium tracking-[0.25em] uppercase text-primary mb-4">
              For Developers
            </p>
            <h2 className="text-4xl md:text-5xl font-bold tracking-tight leading-[1.05] mb-6">
              A Platform,
              <br />
              Not Just a Mod
            </h2>
            <p className="text-muted-foreground text-lg leading-relaxed mb-12 max-w-md">
              Build custom server logic, client-side features, or entirely
              new game modes on a stable, documented foundation.
            </p>

            <div className="space-y-0">
              {apis.map(({ title, desc }, i) => (
                <motion.div
                  key={title}
                  className="py-5 border-t border-border/30 group"
                  initial={{ opacity: 0, x: 20 }}
                  animate={visible ? { opacity: 1, x: 0 } : {}}
                  transition={{
                    duration: 0.5,
                    delay: 0.2 + i * 0.08,
                    ease: [0.22, 1, 0.36, 1],
                  }}
                >
                  <h4 className="text-sm font-semibold text-foreground mb-1 group-hover:text-primary transition-colors duration-200">
                    {title}
                  </h4>
                  <p className="text-sm text-muted-foreground leading-relaxed">
                    {desc}
                  </p>
                </motion.div>
              ))}
              <div className="border-t border-border/30" />
            </div>
          </motion.div>
        </div>
      </div>
    </section>
  );
};

export default ForDevelopers;
